// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceHubDispatcher<THub> where THub : Hub
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceHubDispatcher<THub>> _logger;
        private ServiceOptions _options;
        private IServiceEndpointUtility _serviceEndpointUtility;
        private IServiceConnectionManager _serviceConnectionManager;
        private IClientConnectionManager _clientConnectionManager;
        private IServiceProtocol _serviceProtocol;
        private string _userId;
        private readonly string _name = $"ServiceHubDispatcher<{typeof(THub).FullName}>";

        public ServiceHubDispatcher(IServiceProtocol serviceProtocol,
            IServiceConnectionManager serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IServiceEndpointUtility serviceEndpointUtility,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory)
        {
            _serviceProtocol = serviceProtocol;
            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _serviceEndpointUtility = serviceEndpointUtility;
            _options = options != null ? options.Value : throw new ArgumentNullException(nameof(options));

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<ServiceHubDispatcher<THub>>();
            _userId = GenerateServerName();
        }

        private static string GenerateServerName()
        {
            // Use the machine name for convenient diagnostics, but add a guid to make it unique.
            // Example: MyServerName_02db60e5fab243b890a847fa5c4dcb29
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
        }

        public void Start(ConnectionDelegate connectionDelegate)
        {
            // Simply create a couple of connections which connect to Azure SignalR
            for (var i = 0; i < _options.ConnectionCount; i++)
            {
                var serviceConnection = new ServiceConnection(_serviceProtocol, _clientConnectionManager, _serviceEndpointUtility, _userId, typeof(THub).Name, _loggerFactory, connectionDelegate);
                _serviceConnectionManager.AddServiceConnection(serviceConnection);
            }
            Log.StartingConnection(_logger, _name, _options.ConnectionCount);
            _ = _serviceConnectionManager.StartAsync();
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
