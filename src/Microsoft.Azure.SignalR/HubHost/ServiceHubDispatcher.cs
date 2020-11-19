﻿// Copyright (c) Microsoft. All rights reserved.
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

namespace Microsoft.Azure.SignalR
{
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
            IClientConnectionFactory clientConnectionFactory)
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

            serverLifetimeManager?.Register(ShutdownAsync);
        }

        public void Start(ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig = null)
        {
            // Create connections to Azure SignalR
            var serviceConnection = GetMultiEndpointServiceConnectionContainer(_hubName, connectionDelegate, contextConfig);

            _serviceConnectionManager.SetServiceConnection(serviceConnection);

            Log.StartingConnection(_logger, Name, _options.ConnectionCount);

            _ = _serviceConnectionManager.StartAsync();
        }

        public async Task ShutdownAsync()
        {
            var options = _options.GracefulShutdown;
            if (options.Mode == GracefulShutdownMode.Off)
            {
                return;
            }

            try
            {
                var source = new CancellationTokenSource(_options.GracefulShutdown.Timeout);

                Log.SettingServerOffline(_logger, _hubName);

                await Task.WhenAny(
                    _serviceConnectionManager.OfflineAsync(options.Mode),
                    Task.Delay(Timeout.InfiniteTimeSpan, source.Token)
                );

                Log.TriggeringShutdownHooks(_logger, _hubName);

                await Task.WhenAny(
                    options.OnShutdown(Context),
                    Task.Delay(Timeout.InfiniteTimeSpan, source.Token)
                );

                Log.WaitingClientConnectionsToClose(_logger, _hubName);

                await Task.WhenAny(
                    _clientConnectionManager.WhenAllCompleted(),
                    Task.Delay(Timeout.InfiniteTimeSpan, source.Token)
                );
            }
            catch (OperationCanceledException)
            {
                Log.GracefulShutdownTimeoutExceeded(_logger, _hubName, Convert.ToInt32(_options.GracefulShutdown.Timeout.TotalMilliseconds));
            }

            Log.StoppingServer(_logger, _hubName);
            await _serviceConnectionManager.StopAsync();
        }

        private IMultiEndpointServiceConnectionContainer GetMultiEndpointServiceConnectionContainer(string hub, ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig = null)
        {
            var connectionFactory = new ConnectionFactory(_nameProvider, _loggerFactory);

            var serviceConnectionFactory = GetServiceConnectionFactory(connectionFactory, connectionDelegate, contextConfig);

            var factory = new ServiceConnectionContainerFactory(
                serviceConnectionFactory,
                _serviceEndpointManager,
                _router,
                _options,
                _loggerFactory,
                _options.ServiceScaleTimeout
            );
            return factory.Create(hub);
        }

        internal virtual ServiceConnectionFactory GetServiceConnectionFactory(
            ConnectionFactory connectionFactory,
            ConnectionDelegate connectionDelegate,
            Action<HttpContext> contextConfig)
        { 
            return new ServiceConnectionFactory(
                _serviceProtocol,
                _clientConnectionManager,
                connectionFactory,
                _loggerFactory,
                connectionDelegate,
                _clientConnectionFactory,
                _nameProvider)
            {
                ConfigureContext = contextConfig,
                ShutdownMode = _options.GracefulShutdown.Mode
            };
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
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, "WaitingClientConnectionsToClose"), "[{hubName}] Waiting client connections to close...");

            private static readonly Action<ILogger, string, Exception> _stoppingServer =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, "StoppingServer"), "[{hubName}] Stopping the hub server...");

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

            public static void StoppingServer(ILogger logger, string hubName)
            {
                _stoppingServer(logger, hubName, null);
            }
        }
    }
}
