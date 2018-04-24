// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
    internal class HubHost<THub> : IConnectionFactory where THub : Hub
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<HubHost<THub>> _logger;
        private ServiceOptions _options;
        private HttpConnectionOptions _httpConnectionOptions;
        private IServiceEndpointUtility _serviceEndpointUtility;
        private IServiceConnectionManager _serviceConnectionManager;
        private IClientConnectionManager _clientConnectionManager;
        private IServiceProtocol _serviceProtocol;
        private readonly string _name = $"HubHost<{typeof(THub).FullName}>";

        public HubHost(IServiceProtocol serviceProtocol,
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
            _logger = loggerFactory.CreateLogger<HubHost<THub>>();
        }

        internal void Configure()
        {
            _httpConnectionOptions = new HttpConnectionOptions
            {
                Url = GetServiceUrl(),
                AccessTokenProvider = () => Task.FromResult(_serviceEndpointUtility.GenerateServerAccessToken<THub>()),
                CloseTimeout = TimeSpan.FromSeconds(300),
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true
            };

            // Simply create a couple of connections which connect to Azure SignalR
            for (var i = 0; i < _options.ConnectionNumber; i++)
            {
                var serviceConnection = new ServiceConnection(_serviceProtocol, _clientConnectionManager, this, _loggerFactory);
                _serviceConnectionManager.AddServiceConnection(serviceConnection);
            }
        }

        public void Start(ConnectionDelegate connectionDelegate)
        {
            _logger.LogInformation($"Starting {_name}...");
            _ = _serviceConnectionManager.StartAsync(connectionDelegate);
        }

        private Uri GetServiceUrl()
        {
            return new Uri(_serviceEndpointUtility.GetServerEndpoint<THub>());
        }

        public async Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, CancellationToken cancellationToken = default)
        {
            var httpConnection = new HttpConnection(_httpConnectionOptions, _loggerFactory);
            
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
    }
}
