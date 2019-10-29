// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
        private readonly IServerNameProvider _nameProvider;

        internal bool EnableGracefulShutdown
        {
            get { return _options.EnableGracefulShutdown; }
        }

        internal TimeSpan ShutdownTimeout
        {
            get { return _options.ServerShutdownTimeout; }
        }

        public ServiceHubDispatcher(
            IServiceProtocol serviceProtocol,
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IServiceEndpointManager serviceEndpointManager,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory,
            IEndpointRouter router,
            IServerNameProvider nameProvider,
            IClientConnectionFactory clientConnectionFactory)
        {
            _serviceProtocol = serviceProtocol;
            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _serviceEndpointManager = serviceEndpointManager;
            _options = options != null ? options.Value : throw new ArgumentNullException(nameof(options));

            _router = router ?? throw new ArgumentNullException(nameof(router));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<ServiceHubDispatcher<THub>>();
            _clientConnectionFactory = clientConnectionFactory;
            _nameProvider = nameProvider;
            _hubName = typeof(THub).Name;
        }

        public void Start(ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig = null)
        {
            // Simply create a couple of connections which connect to Azure SignalR
            var serviceConnection = GetMultiEndpointServiceConnectionContainer(_hubName, connectionDelegate, contextConfig);

            _serviceConnectionManager.SetServiceConnection(serviceConnection);

            Log.StartingConnection(_logger, Name, _options.ConnectionCount);

            _ = _serviceConnectionManager.StartAsync();
        }

        public Task Shutdown(TimeSpan timeout)
        {
            return _serviceConnectionManager.ShutdownAsync(timeout);
        }

        private MultiEndpointServiceConnectionContainer GetMultiEndpointServiceConnectionContainer(string hub, ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig = null)
        {
            var connectionFactory = new ConnectionFactory(_nameProvider, _loggerFactory);
            var serviceConnectionFactory = new ServiceConnectionFactory(_serviceProtocol, _clientConnectionManager, connectionFactory, _loggerFactory, connectionDelegate, _clientConnectionFactory);
            serviceConnectionFactory.ConfigureContext = contextConfig;
            return new MultiEndpointServiceConnectionContainer(serviceConnectionFactory, hub, _options.ConnectionCount, _serviceEndpointManager, _router, _nameProvider, _loggerFactory);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, int, Exception> _startingConnection =
                LoggerMessage.Define<string, int>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Starting {name} with {connectionNumber} connections...");

            public static void StartingConnection(ILogger logger, string name, int connectionNumber)
            {
                _startingConnection(logger, name, connectionNumber, null);
            }
        }
    }
}
