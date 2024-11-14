// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;


#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Connections;
#endif

namespace Microsoft.Azure.SignalR;

internal class ServiceHubDispatcher<THub> where THub : Hub
{
    private static readonly string Name = $"ServiceHubDispatcher<{typeof(THub).FullName}>";

    private IHubContext<THub> Context { get; }

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ServiceHubDispatcher<THub>> _logger;
    private readonly ServiceOptions _options;
    private readonly IServiceEndpointManager _serviceEndpointManager;
    private readonly IServiceConnectionManager<THub> _serviceConnectionManager;
    private readonly IClientConnectionManager _clientConnectionManager;
    private readonly IServiceProtocol _serviceProtocol;
    private readonly IClientConnectionFactory _clientConnectionFactory;
    private readonly IEndpointRouter _router;
    private readonly string _hubName;
    private readonly IServiceEventHandler _serviceEventHandler;
    private readonly IClientInvocationManager _clientInvocationManager;
    private readonly IHubProtocolResolver _hubProtocolResolver;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IServiceProvider _serviceProvider;
#if NET8_0_OR_GREATER
    private readonly HttpConnectionDispatcherOptions _dispatcherOptions;
#endif

    protected readonly IServerNameProvider _nameProvider;

    public ServiceHubDispatcher(
        IServiceProtocol serviceProtocol,
        IHubContext<THub> context,
        IServiceConnectionManager<THub> serviceConnectionManager,
        IClientConnectionManager clientConnectionManager,
        IServiceEndpointManager serviceEndpointManager,
        IOptions<ServiceOptions> options,
        ILoggerFactory loggerFactory,
        IEndpointRouter router,
        IServerNameProvider nameProvider,
        ServerLifetimeManager serverLifetimeManager,
        IClientConnectionFactory clientConnectionFactory,
        IClientInvocationManager clientInvocationManager,
        IServiceEventHandler serviceEventHandler,
        IHubProtocolResolver hubProtocolResolver,
        IConnectionFactory connectionFactory,
        IServiceProvider serviceProvider
#if NET8_0_OR_GREATER
        ,
        EndpointDataSource endpointDataSource
#endif
        )
    {
        _serviceProtocol = serviceProtocol;
        _serviceConnectionManager = serviceConnectionManager;
        _clientConnectionManager = clientConnectionManager;
        _serviceEndpointManager = serviceEndpointManager;
        _options = options != null ? options.Value : throw new ArgumentNullException(nameof(options));

        Context = context;

        _router = router ?? throw new ArgumentNullException(nameof(router));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<ServiceHubDispatcher<THub>>();
        _clientConnectionFactory = clientConnectionFactory;
        _nameProvider = nameProvider;
        _hubName = typeof(THub).Name;
        _serviceEventHandler = serviceEventHandler;
        _clientInvocationManager = clientInvocationManager;

        serverLifetimeManager?.Register(ShutdownAsync);
        _hubProtocolResolver = hubProtocolResolver;
        _connectionFactory = connectionFactory;
        _serviceProvider = serviceProvider;
#if NET8_0_OR_GREATER
        _dispatcherOptions = GetDispatcherOptions(endpointDataSource, typeof(THub));
#endif
    }

#if NET8_0_OR_GREATER
    private static HttpConnectionDispatcherOptions GetDispatcherOptions(EndpointDataSource source, Type hubType)
    {
        if (source != null)
        {
            foreach (var endpoint in source.Endpoints)
            {
                var metaData = endpoint.Metadata;
                if (metaData.GetMetadata<HubMetadata>()?.HubType == hubType)
                {
                    var options = metaData.GetMetadata<HttpConnectionDispatcherOptions>();
                    if (options != null)
                    {
                        return options;
                    }
                }
            }
        }
        // It's not expected to go here in production environment. Return a value for test.
        return new();
    }
#endif

    public void Start(ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig = null)
    {
        // Create connections to Azure SignalR
        var serviceConnection = GetServiceConnectionContainer(connectionDelegate, contextConfig);

        _serviceConnectionManager.SetServiceConnection(serviceConnection);

        Log.StartingConnection(_logger, Name, _options.InitialHubServerConnectionCount);

        _ = _serviceConnectionManager.StartAsync();
    }

    public async Task ShutdownAsync()
    {
        var options = _options.GracefulShutdown;
        try
        {
            var source = new CancellationTokenSource(options.Timeout);
            await GracefulShutdownAsync(options, source.Token).OrCancelAsync(source.Token);
        }
        catch (OperationCanceledException)
        {
            Log.GracefulShutdownTimeoutExceeded(_logger, _hubName, Convert.ToInt32(options.Timeout.TotalMilliseconds));
        }

        Log.StoppingServer(_logger, _hubName);
        await _serviceConnectionManager.StopAsync();
    }

    private async Task GracefulShutdownAsync(GracefulShutdownOptions options, CancellationToken cancellationToken)
    {
        Log.SettingServerOffline(_logger, _hubName);
        await _serviceConnectionManager.OfflineAsync(options.Mode, cancellationToken);

        if (options.Mode == GracefulShutdownMode.Off)
        {
            // By default we directly close all the client connections
            Log.CloseClientConnections(_logger, _hubName);
            await _serviceConnectionManager.CloseClientConnections(cancellationToken);
        }
        else
        {
            Log.TriggeringShutdownHooks(_logger, _hubName);
            await options.OnShutdown(Context);
        }

        Log.WaitingClientConnectionsToClose(_logger, _hubName);
        await _clientConnectionManager.WhenAllCompleted();
    }

    private IServiceConnectionContainer GetServiceConnectionContainer(ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig = null)
    {
        var factory = _serviceProvider.GetService<IServiceConnectionContainerFactory>();
        if (factory == null)
        {
            var allowStatefulReconnects = _options.AllowStatefulReconnects ??
#if NET8_0_OR_GREATER
                    _dispatcherOptions.AllowStatefulReconnects;
#else
                false;
#endif

            var serviceConnectionFactory = _serviceProvider.GetService<IServiceConnectionFactory>();
            if (serviceConnectionFactory == null)
            {
                var scf = GetServiceConnectionFactory(connectionDelegate);
                scf.ConfigureContext = contextConfig;
                scf.ShutdownMode = _options.GracefulShutdown.Mode;
                scf.AllowStatefulReconnects = allowStatefulReconnects;
                serviceConnectionFactory = scf;
            }
            factory = new ServiceConnectionContainerFactory(
                serviceConnectionFactory,
                _serviceEndpointManager,
                _router,
                _options,
                _loggerFactory
            );
        }
        return factory.Create(_hubName, _options.ServiceScaleTimeout);
    }

    internal virtual ServiceConnectionFactory GetServiceConnectionFactory(ConnectionDelegate connectionDelegate)
    {
        return ActivatorUtilities.CreateInstance<ServiceConnectionFactory>(_serviceProvider, connectionDelegate);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, int, Exception> _startingConnection =
            LoggerMessage.Define<string, int>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Starting {name} with {connectionNumber} connections...");

        private static readonly Action<ILogger, string, int, Exception> _gracefulShutdownTimeoutExceeded =
            LoggerMessage.Define<string, int>(LogLevel.Warning, new EventId(2, "GracefulShutdownTimeoutExceeded"), "[{hubName}] Timeout({timeoutInMs}ms) reached, existing client connections will be dropped immediately.");

        private static readonly Action<ILogger, string, Exception> _settingServerOffline =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "SettingServerOffline"), "[{hubName}] Setting the hub server offline...");

        private static readonly Action<ILogger, string, Exception> _triggeringShutdownHooks =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, "TriggeringShutdownHooks"), "[{hubName}] Triggering shutdown hooks...");

        private static readonly Action<ILogger, string, Exception> _waitingClientConnectionsToClose =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, "WaitingClientConnectionsToClose"), "[{hubName}] Waiting for client connections to close...");

        private static readonly Action<ILogger, string, Exception> _stoppingServer =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, "StoppingServer"), "[{hubName}] Stopping the hub server...");

        private static readonly Action<ILogger, string, Exception> _closeClientConnections =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(7, "CloseClientConnections"), "[{hubName}] Closing client connections...");

        public static void StartingConnection(ILogger logger, string name, int connectionNumber)
        {
            _startingConnection(logger, name, connectionNumber, null);
        }

        public static void GracefulShutdownTimeoutExceeded(ILogger logger, string hubName, int timeoutInMs)
        {
            _gracefulShutdownTimeoutExceeded(logger, hubName, timeoutInMs, null);
        }

        public static void SettingServerOffline(ILogger logger, string hubName)
        {
            _settingServerOffline(logger, hubName, null);
        }

        public static void TriggeringShutdownHooks(ILogger logger, string hubName)
        {
            _triggeringShutdownHooks(logger, hubName, null);
        }

        public static void WaitingClientConnectionsToClose(ILogger logger, string hubName)
        {
            _waitingClientConnectionsToClose(logger, hubName, null);
        }

        public static void CloseClientConnections(ILogger logger, string hubName)
        {
            _closeClientConnections(logger, hubName, null);
        }

        public static void StoppingServer(ILogger logger, string hubName)
        {
            _stoppingServer(logger, hubName, null);
        }
    }
}
