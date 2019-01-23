// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceHubDispatcher
    {
        private readonly ServiceOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceHubDispatcher> _logger;
        private readonly IReadOnlyList<string> _hubNames;
        private readonly IServiceConnectionManager _serviceConnectionManager;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly IServiceProtocol _protocol;
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly IEndpointRouter _router;
        private readonly string _name;

        public ServiceHubDispatcher(IReadOnlyList<string> hubNames, IServiceProtocol protocol,
            IServiceConnectionManager serviceConnectionManager, IClientConnectionManager clientConnectionManager,
            IServiceEndpointManager serviceEndpointManager,
            IEndpointRouter router,
            IOptions<ServiceOptions> options, ILoggerFactory loggerFactory)
        {
            _hubNames = hubNames;
            _name = $"{nameof(ServiceHubDispatcher)}[{string.Join(",", hubNames)}]";
            _loggerFactory = loggerFactory;
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _serviceConnectionManager = serviceConnectionManager ?? throw new ArgumentNullException(nameof(serviceConnectionManager));
            _clientConnectionManager = clientConnectionManager ?? throw new ArgumentNullException(nameof(clientConnectionManager));
            _options = options?.Value;
            _serviceEndpointManager = serviceEndpointManager ?? throw new ArgumentNullException(nameof(serviceEndpointManager));
            _logger = _loggerFactory.CreateLogger<ServiceHubDispatcher>();
        }

        public Task StartAsync()
        {
            _serviceConnectionManager.Initialize(hub => GetMultiEndpointServiceConnectionContainer(hub));

            Log.StartingConnection(_logger, _name, _options.ConnectionCount, _hubNames.Count);

            return _serviceConnectionManager.StartAsync();
        }

        private MultiEndpointServiceConnectionContainer GetMultiEndpointServiceConnectionContainer(string hub)
        {
            var serviceConnectionFactory = new ServiceConnectionFactory(_protocol, _clientConnectionManager, _loggerFactory);
            return new MultiEndpointServiceConnectionContainer(serviceConnectionFactory, hub, _options.ConnectionCount, _serviceEndpointManager, _router, _loggerFactory);
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
