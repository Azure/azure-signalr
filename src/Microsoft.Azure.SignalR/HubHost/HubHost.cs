// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    public class HubHost<THub> where THub : Hub
    {
        private readonly List<ServiceConnection> _cloudConnections = new List<ServiceConnection>();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<HubHost<THub>> _logger;

        private HubHostOptions _options;
        private EndpointProvider _endpointProvider;
        private TokenProvider _tokenProvider;
        private IServiceConnectionManager _serviceConnectionManager;
        private IClientConnectionManager _clientConnectionManager;
        private readonly string _name = $"HubHost<{typeof(THub).FullName}>";

        public HubHost(IServiceConnectionManager serviceConnectionManager, IClientConnectionManager clientConnectionManager, IOptions<HubHostOptions> options, ILoggerFactory loggerFactory)
        {
            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _options = options != null ? options.Value : throw new ArgumentNullException(nameof(options));

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<HubHost<THub>>();
        }

        internal void Configure(EndpointProvider endpointProvider, TokenProvider tokenProvider,
            HubHostOptions options = null)
        {
            if (_endpointProvider != null || _tokenProvider != null)
            {
                throw new InvalidOperationException(
                    $"{typeof(THub).FullName} can only bind with one Azure SignalR instance. Binding to multiple instances is forbidden.");
            }

            _endpointProvider = endpointProvider ?? throw new ArgumentNullException(nameof(endpointProvider));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            if (options != null) _options = options;

            var serviceUrl = GetServiceUrl();
            var httpOptions = new HttpOptions
            {
                AccessTokenFactory = () => _tokenProvider.GenerateServerAccessToken<THub>()
            };

            // Simply create a couple of connections which connect to Azure SignalR
            for (var i = 0; i < _options.ConnectionNumber; i++)
            {
                var serviceConnection = CreateServiceConnection(serviceUrl, httpOptions);
                _serviceConnectionManager.AddServiceConnection(serviceConnection);
            }
        }

        public async Task StartAsync(ConnectionDelegate connectionDelegate)
        {
            _logger.LogInformation($"Starting {_name}...");
            await _serviceConnectionManager.StartAllServiceConnection(connectionDelegate);
        }

        private Uri GetServiceUrl()
        {
            return new Uri(_endpointProvider.GetServerEndpoint<THub>());
        }

        private ServiceConnection CreateServiceConnection(Uri serviceUrl, HttpOptions httpOptions)
        {
            var httpConnection = new HttpConnection(serviceUrl, HttpTransportType.WebSockets, _loggerFactory, httpOptions);
            return new ServiceConnection(_clientConnectionManager, serviceUrl, httpConnection, _loggerFactory);
        }
    }
}
