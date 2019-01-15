// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
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

        public ServiceHubDispatcher(IServiceProtocol serviceProtocol,
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IServiceEndpointManager serviceEndpointManager,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory,
            IEndpointRouter router,
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
            _hubName = typeof(THub).Name;
        }

        public void Start(ConnectionDelegate connectionDelegate)
        {
            // Simply create a couple of connections which connect to Azure SignalR
            var serviceConnection = GetMultiEndpointServiceConnectionContainer(_hubName, connectionDelegate);

            _serviceConnectionManager.SetServiceConnection(serviceConnection);

            Log.StartingConnection(_logger, Name, _options.ConnectionCount);
            _ = _serviceConnectionManager.StartAsync();
        }

        //private MultiEndpointServiceConnectionContainer GetMultiEndpointServiceConnectionContainer(string hub, ConnectionDelegate connectionDelegate)
        //{
        //    return new MultiEndpointServiceConnectionContainer(
        //        (t, f) => GetServiceConnection(connectionDelegate, t, f),
        //        hub, _options.ConnectionCount,
        //        _serviceEndpointManager, _router, _loggerFactory);
        //}

        private MultiEndpointServiceConnectionContainer GetMultiEndpointServiceConnectionContainer(string hub, ConnectionDelegate connectionDelegate)
        {
            var serviceConnectionFactory = new ServiceConnectionFactory(_serviceProtocol, _clientConnectionManager, null, _loggerFactory, connectionDelegate,_clientConnectionFactory);
            return new MultiEndpointServiceConnectionContainer(serviceConnectionFactory, hub, _options.ConnectionCount, _serviceEndpointManager, _router, _loggerFactory);
        }

        private ServiceConnection GetServiceConnection(ConnectionDelegate connectionDelegate, ServerConnectionType type, IConnectionFactory factory)
        {
            return new ServiceConnection(_serviceProtocol, _clientConnectionManager, factory,
                _loggerFactory, connectionDelegate, _clientConnectionFactory,
                Guid.NewGuid().ToString(), type);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, int, Exception> _startingConnection =
                LoggerMessage.Define<string, int>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Staring {name} with {connectionNumber} connections...");

            public static void StartingConnection(ILogger logger, string name, int connectionNumber)
            {
                _startingConnection(logger, name, connectionNumber, null);
            }
        }
    }
}
