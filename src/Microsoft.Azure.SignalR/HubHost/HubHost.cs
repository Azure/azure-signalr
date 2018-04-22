// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
    internal class HubHost<THub> where THub : Hub
    {
        private readonly List<ServiceConnection> _cloudConnections = new List<ServiceConnection>();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<HubHost<THub>> _logger;

        private ServiceOptions _options;
        private IConnectionServiceProvider _connectionServiceProvider;
        private IServiceConnectionManager _serviceConnectionManager;
        private IClientConnectionManager _clientConnectionManager;
        private IServiceProtocol _serviceProtocol;
        private readonly string _name = $"HubHost<{typeof(THub).FullName}>";

        public HubHost(IServiceProtocol serviceProtocol,
            IServiceConnectionManager serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IConnectionServiceProvider connectionServiceProvider,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory)
        {
            _serviceProtocol = serviceProtocol;
            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _connectionServiceProvider = connectionServiceProvider;
            _options = options != null ? options.Value : throw new ArgumentNullException(nameof(options));

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<HubHost<THub>>();
        }

        internal void Configure()
        {
            var serviceUrl = GetServiceUrl();
            var httpOptions = new HttpOptions
            {
                AccessTokenFactory = () => _connectionServiceProvider.GenerateServerAccessToken<THub>(),
                CloseTimeout = TimeSpan.FromSeconds(300)
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
            return new Uri(_connectionServiceProvider.GetServerEndpoint<THub>());
        }

        private ServiceConnection CreateServiceConnection(Uri serviceUrl, HttpOptions httpOptions)
        {
            var httpConnection = new HttpConnection(serviceUrl, HttpTransportType.WebSockets, _loggerFactory, httpOptions);
            return new ServiceConnection(_serviceProtocol, _clientConnectionManager, serviceUrl, httpConnection, _loggerFactory);
        }
    }
}
