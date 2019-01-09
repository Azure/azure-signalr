// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.AspNet
{
    /// <summary>
    /// TODO: This factory class is responsible for creating, disposing and starting the server-service connections
    /// </summary>
    internal class ConnectionFactory : IConnectionFactory
    {
        private readonly ServiceOptions _options;
        private readonly HubConfiguration _config;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ConnectionFactory> _logger;
        private readonly IReadOnlyList<string> _hubNames;
        private readonly IServiceConnectionManager _serviceConnectionManager;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly IServiceProtocol _protocol;
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly string _name;
        private readonly string _userId;
        
        // Align the header key with ASP.Net core.
        private static Dictionary<string, string> CustomHeader = new Dictionary<string, string> { { "Asrs-User-Agent", ProductInfo.GetProductInfo() } };

        public ConnectionFactory(IReadOnlyList<string> hubNames, HubConfiguration hubConfig, IServiceProtocol protocol,
            IServiceConnectionManager serviceConnectionManager, IClientConnectionManager clientConnectionManager,
            IServiceEndpointManager serviceEndpointManager,
            IOptions<ServiceOptions> options, ILoggerFactory loggerFactory)
        {
            _config = hubConfig;
            _hubNames = hubNames;
            _name = $"{nameof(ConnectionFactory)}[{string.Join(",", hubNames)}]";
            _userId = GenerateServerName();
            _loggerFactory = loggerFactory;
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _serviceConnectionManager = serviceConnectionManager ?? throw new ArgumentNullException(nameof(serviceConnectionManager));
            _clientConnectionManager = clientConnectionManager ?? throw new ArgumentNullException(nameof(clientConnectionManager));
            _options = options?.Value;
            _serviceEndpointManager = serviceEndpointManager ?? throw new ArgumentNullException(nameof(serviceEndpointManager));
            _logger = _loggerFactory.CreateLogger<ConnectionFactory>();
        }

        public async Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, string hubName, CancellationToken cancellationToken = default)
        {
            var endpoints = _serviceEndpointManager.GetAvailableEndpoints();
            if (endpoints.Count == 0)
            {
                throw new AzureSignalRException("No available endpoints.");
            }

            // TODO: support multiple endpoints
            var provider = _serviceEndpointManager.GetEndpointProvider(endpoints[0]);

            var httpConnectionOptions = new HttpConnectionOptions
            {
                Url = GetServiceUrl(connectionId, hubName, provider),
                AccessTokenProvider = () => Task.FromResult(provider.GenerateServerAccessToken(hubName, _userId)),
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true,
                Headers = CustomHeader
            };
            var httpConnection = new HttpConnection(httpConnectionOptions, _loggerFactory);
            try
            {
                await httpConnection.StartAsync(transferFormat);
                return httpConnection;
            }
            catch
            {
                await httpConnection.DisposeAsync();
                throw;
            }
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            if (connection == null)
            {
                return Task.CompletedTask;
            }
            
            return ((HttpConnection)connection).DisposeAsync();
        }

        public Task StartAsync()
        {
            _serviceConnectionManager.Initialize(
                (hub, container) => GetServiceConnection(hub, ServerConnectionType.Default, String.Empty, target => OnDemandGenerator(container, hub, target)),
                _options.ConnectionCount);

            Log.StartingConnection(_logger, _name, _options.ConnectionCount, _hubNames.Count);

            return _serviceConnectionManager.StartAsync();
        }

        private Task OnDemandGenerator(IServiceConnectionContainer container, string hub, string target)
        {
            var connection = GetServiceConnection(hub, ServerConnectionType.OnDemand, target, innerTarget => OnDemandGenerator(container, hub, target));
            container.AddServiceConnection(connection);
            return connection.StartAsync();
        }

        private ServiceConnection GetServiceConnection(string hub, ServerConnectionType type, string target, Func<string, Task> onDemandGenerator)
        {
            return new ServiceConnection(hub, Guid.NewGuid().ToString(), _protocol, this, _clientConnectionManager,
                _logger, target, onDemandGenerator, type);
        }

        private Uri GetServiceUrl(string connectionId, string hubName, IServiceEndpointProvider provider)
        {
            var baseUri = new UriBuilder(provider.GetServerEndpoint(hubName));
            var query = "cid=" + connectionId;
            if (baseUri.Query != null && baseUri.Query.Length > 1)
            {
                baseUri.Query = baseUri.Query.Substring(1) + "&" + query;
            }
            else
            {
                baseUri.Query = query;
            }
            return baseUri.Uri;
        }

        private static string GenerateServerName()
        {
            // Use the machine name for convenient diagnostics, but add a guid to make it unique.
            // Example: MyServerName_02db60e5fab243b890a847fa5c4dcb29
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
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
