// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceHubDispatcher<THub> : IConnectionFactory where THub : Hub
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ServiceHubDispatcher<THub>> _logger;
        private readonly ServiceOptions _options;
        private readonly IServiceEndpointUtility _serviceEndpointUtility;
        private readonly IServiceConnectionManager<THub> _serviceConnectionManager;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionFactory _clientConnectionFactory;
        private readonly string _userId;
        private static readonly string Name = $"ServiceHubDispatcher<{typeof(THub).FullName}>";
        private static Dictionary<string, string> CustomHeader = ProductInfo.ToHeader();

        public ServiceHubDispatcher(IServiceProtocol serviceProtocol,
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IServiceEndpointUtility serviceEndpointUtility,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory,
            IClientConnectionFactory clientConnectionFactory)
        {
            _serviceProtocol = serviceProtocol;
            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _serviceEndpointUtility = serviceEndpointUtility;
            _options = options != null ? options.Value : throw new ArgumentNullException(nameof(options));

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<ServiceHubDispatcher<THub>>();
            _clientConnectionFactory = clientConnectionFactory;
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
                var serviceConnection = new ServiceConnection(_serviceProtocol, _clientConnectionManager, this, _loggerFactory, connectionDelegate, _clientConnectionFactory, Guid.NewGuid().ToString());
                _serviceConnectionManager.AddServiceConnection(serviceConnection);
            }
            Log.StartingConnection(_logger, Name, _options.ConnectionCount);
            _ = _serviceConnectionManager.StartAsync();
        }

        private Uri GetServiceUrl(string connectionId)
        {
            var baseUri = new UriBuilder(_serviceEndpointUtility.GetServerEndpoint<THub>());
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

        public async Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, CancellationToken cancellationToken = default)
        {
            var httpConnectionOptions = new HttpConnectionOptions
            {
                Url = GetServiceUrl(connectionId),
                AccessTokenProvider = () => Task.FromResult(_serviceEndpointUtility.GenerateServerAccessToken<THub>(_userId)),
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
            return ((HttpConnection)connection).DisposeAsync();
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
