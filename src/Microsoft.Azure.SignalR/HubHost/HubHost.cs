// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Client;
using Microsoft.AspNetCore.Sockets.Client.Http;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    public class HubHost<THub> where THub : Hub
    {
        private readonly List<ServiceConnection<THub>> _serviceConnections = new List<ServiceConnection<THub>>();

        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly IHubProtocolResolver _protocolResolver;
        private readonly HubDispatcher<THub> _hubDispatcher;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<HubHost<THub>> _logger;

        private HubHostOptions _options;
        private EndpointProvider _endpointProvider;
        private TokenProvider _tokenProvider;

        private readonly string _name = $"HubHost<{typeof(THub).FullName}>";

        public HubHost(HubLifetimeManager<THub> lifetimeManager,
            IHubProtocolResolver protocolResolver,
            HubDispatcher<THub> hubDispatcher,
            IOptions<HubHostOptions> options,
            ILoggerFactory loggerFactory)
        {
            _lifetimeManager = lifetimeManager ?? throw new ArgumentNullException(nameof(lifetimeManager));
            _protocolResolver = protocolResolver ?? throw new ArgumentNullException(nameof(protocolResolver));
            _hubDispatcher = hubDispatcher ?? throw new ArgumentNullException(nameof(hubDispatcher));
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
                    $"{typeof(THub).Name} can only be used with one Azure SignalR instance. It can't be used with multiple Azure SignalR instances");
            }

            _endpointProvider = endpointProvider ?? throw new ArgumentNullException(nameof(endpointProvider));
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
            if (options != null) _options = options;

            var serviceUrl = GetServiceUrl();
            var httpOptions = new HttpOptions
            {
                AccessTokenFactory = () => _tokenProvider.GenerateServerAccessToken<THub>()
            };

            for (var i = 0; i < _options.ConnectionNumber; i++)
            {
                var serviceConnection = CreateServiceConnection(serviceUrl, httpOptions);
                _serviceConnections.Add(serviceConnection);
            }
        }

        public async Task StartAsync()
        {
            try
            {
                _logger.LogInformation($"Starting {_name}...");
                var tasks = _serviceConnections.Select(c => c.StartAsync());
                await Task.WhenAll(tasks);
                _logger.LogInformation($"Started {_name}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to start {_name}.");
            }
        }

        public async Task StopAsync()
        {
            try
            {
                _logger.LogInformation($"Stopping {_name}...");
                var tasks = _serviceConnections.Select(c => c.StopAsync());
                await Task.WhenAll(tasks);
                _logger.LogInformation($"Stopped {_name}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to stop {_name}.");
            }
        }

        #region Private Methods

        private Uri GetServiceUrl()
        {
            return new Uri(_endpointProvider.GetServerEndpoint<THub>());
        }

        private ServiceConnection<THub> CreateServiceConnection(Uri serviceUrl, HttpOptions httpOptions)
        {
            var httpConnection = new HttpConnection(serviceUrl, TransportType.WebSockets, _loggerFactory, httpOptions);
            var protocolName = _options.ProtocolType == ProtocolType.Text ? "json" : "messagepack";
            var protocol = _protocolResolver.GetProtocol(protocolName, null);
            return new ServiceConnection<THub>(httpConnection, protocol, _lifetimeManager, _hubDispatcher, _loggerFactory);
        }

        #endregion
    }
}
