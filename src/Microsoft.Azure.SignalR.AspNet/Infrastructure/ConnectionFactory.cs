// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ConnectionFactory
    {
        private readonly ServiceOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ConnectionFactory> _logger;
        private readonly IReadOnlyList<string> _hubNames;
        private readonly IServiceConnectionManager _serviceConnectionManager;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly IServiceProtocol _protocol;
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly string _name;

        public ConnectionFactory(IReadOnlyList<string> hubNames, IServiceProtocol protocol,
            IServiceConnectionManager serviceConnectionManager, IClientConnectionManager clientConnectionManager,
            IServiceEndpointManager serviceEndpointManager,
            IOptions<ServiceOptions> options, ILoggerFactory loggerFactory)
        {
            _hubNames = hubNames;
            _name = $"{nameof(ConnectionFactory)}[{string.Join(",", hubNames)}]";
            _loggerFactory = loggerFactory;
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _serviceConnectionManager = serviceConnectionManager ?? throw new ArgumentNullException(nameof(serviceConnectionManager));
            _clientConnectionManager = clientConnectionManager ?? throw new ArgumentNullException(nameof(clientConnectionManager));
            _options = options?.Value;
            _serviceEndpointManager = serviceEndpointManager ?? throw new ArgumentNullException(nameof(serviceEndpointManager));
            _logger = _loggerFactory.CreateLogger<ConnectionFactory>();
        }

        public Task StartAsync()
        {
            var endpoints = _serviceEndpointManager.GetAvailableEndpoints();
            if (endpoints.Count == 0)
            {
                throw new AzureSignalRException("No available endpoints.");
            }

            // TODO: support multiple endpoints
            var provider = _serviceEndpointManager.GetEndpointProvider(endpoints[0]);

            _serviceConnectionManager.Initialize(
                (hub, container) =>
                {
                    var connectionFactory = new ServiceConnectionFactory(hub, provider, _loggerFactory);
                    return GetServiceConnection(hub, connectionFactory, ServerConnectionType.Default, string.Empty,
                        target => OnDemandGenerator(hub, container, connectionFactory, target));
                },
                _options.ConnectionCount);

            Log.StartingConnection(_logger, _name, _options.ConnectionCount, _hubNames.Count);

            return _serviceConnectionManager.StartAsync();
        }

        private Task OnDemandGenerator(string hub, IServiceConnectionContainer container, ServiceConnectionFactory factory, string target)
        {
            var connection = GetServiceConnection(hub, factory, ServerConnectionType.OnDemand, target, innerTarget => OnDemandGenerator(hub, container, factory, innerTarget));
            container.AddServiceConnection(connection);
            return connection.StartAsync();
        }

        private ServiceConnection GetServiceConnection(string hub, ServiceConnectionFactory factory, ServerConnectionType type, string target, Func<string, Task> onDemandGenerator)
        {
            return new ServiceConnection(hub, Guid.NewGuid().ToString(), _protocol, factory, _clientConnectionManager,
                _logger, target, onDemandGenerator, type);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, int, int, Exception> _startingConnection =
                LoggerMessage.Define<string, int, int>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Starting {name} with {hubCount} hubs and {connectionCount} per hub connections...");

            public static void StartingConnection(ILogger logger, string name, int connectionCount, int hubCount)
            {
                _startingConnection(logger, name, connectionCount, hubCount, null);
            }
        }
    }
}
