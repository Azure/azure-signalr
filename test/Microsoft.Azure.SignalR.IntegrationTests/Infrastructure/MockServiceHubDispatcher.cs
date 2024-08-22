// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal class MockServiceHubDispatcher<THub> : ServiceHubDispatcher<THub> 
        where THub : Hub 
    {
        private ILoggerFactory _loggerFactory;
        private IClientConnectionManager _clientConnectionManager;
        private IServiceProtocol _serviceProtocol;
        private IClientConnectionFactory _clientConnectionFactory;
        private IClientInvocationManager _clientInvocationManager;
        private IHubProtocolResolver _hubProtocolResolver;

        public MockServiceHubDispatcher(
            IServiceProtocol serviceProtocol,
            IHubContext<THub> context,
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IClientInvocationManager clientInvocationManager,
            IServiceEndpointManager serviceEndpointManager,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory,
            IEndpointRouter router,
            IServerNameProvider nameProvider,
            ServerLifetimeManager serverLifetimeManager,
            IClientConnectionFactory clientConnectionFactory,
            IHubProtocolResolver hubProtocolResolver) : base(
                serviceProtocol,
                context,
                serviceConnectionManager,
                clientConnectionManager,
                serviceEndpointManager,
                options,
                loggerFactory,
                router,
                nameProvider,
                serverLifetimeManager,
                clientConnectionFactory,
                clientInvocationManager,
                null,
                hubProtocolResolver,
                null)
        {
            MockService = new ConnectionTrackingMockService();

            // just store copies of these locally to keep the base class' accessor modifiers intact
            _loggerFactory = loggerFactory;
            _clientConnectionManager = clientConnectionManager;
            _serviceProtocol = serviceProtocol;
            _clientConnectionFactory = clientConnectionFactory;
            _clientInvocationManager = clientInvocationManager;
            _hubProtocolResolver = hubProtocolResolver;
        }

        internal override ServiceConnectionFactory GetServiceConnectionFactory(
            ConnectionFactory connectionFactory, ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig
            ) => new MockServiceConnectionFactory(MockService, _serviceProtocol, _clientConnectionManager, connectionFactory, _loggerFactory, connectionDelegate, _clientConnectionFactory, _clientInvocationManager, _nameProvider, _hubProtocolResolver);

        // this is the gateway for the tests to control the mock service side
        public IMockService MockService { 
            get; 
            private set; 
        }
    }
}
