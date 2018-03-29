// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Client;
using Microsoft.AspNetCore.Sockets.Client.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR
{
    public class HubHost<THub> where THub : Hub
    {
        private readonly List<CloudConnection<THub>> _cloudConnections = new List<CloudConnection<THub>>();
        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly IHubProtocolResolver _protocolResolver;
        private readonly HubDispatcher<THub> _hubDispatcher;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<HubHost<THub>> _logger;
        // Protocol Reader/Writer shared between all HubConnectionContexts
        private readonly IHubProtocol _jsonProtocol;
        private readonly IHubProtocol _messagePackProtocol;
        private readonly string _name = $"HubHost<{typeof(THub).FullName}>";

        private HubHostOptions _options;
        private EndpointProvider _endpointProvider;
        private TokenProvider _tokenProvider;

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
            _logger = _loggerFactory.CreateLogger<HubHost<THub>>();

            _jsonProtocol = _protocolResolver.GetProtocol(JsonHubProtocol.ProtocolName, null, null);
            _messagePackProtocol = _protocolResolver.GetProtocol(MessagePackHubProtocol.ProtocolName, null, null);
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
                var cloudConnection = CreateCloudConnection(serviceUrl, httpOptions);
                _cloudConnections.Add(cloudConnection);
            }
        }

        public async Task StartAsync()
        {
            _logger.LogInformation($"Starting {_name}...");
            var tasks = _cloudConnections.Select(c => c.StartAsync());
            await Task.WhenAll(tasks);
        }

        private Uri GetServiceUrl()
        {
            return new Uri(_endpointProvider.GetServerEndpoint<THub>());
        }

        private CloudConnection<THub> CreateCloudConnection(Uri serviceUrl, HttpOptions httpOptions)
        {
            var httpConnection = new HttpConnection(serviceUrl, TransportType.WebSockets, _loggerFactory, httpOptions);
            // Apply the customized protocol between SignalR Service and SignalR App Server.
            var protocolName = _options.ProtocolType == TransferFormat.Text ?
                JsonHubProtocolWrapper.ProtocolName : MessagePackHubProtocolWrapper.ProtocolName;
            var protocol = _protocolResolver.GetProtocol(protocolName, null, null);
            return new CloudConnection<THub>(httpConnection, protocol, _options, _lifetimeManager,
                _hubDispatcher, _loggerFactory, _protocolResolver, _jsonProtocol, _messagePackProtocol);
        }
    }
}
