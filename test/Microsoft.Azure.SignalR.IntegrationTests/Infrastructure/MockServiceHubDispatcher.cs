﻿// Copyright (c) Microsoft. All rights reserved.
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
        private IClientResultsManager _clientResultsManager;

        public MockServiceHubDispatcher(
            IServiceProtocol serviceProtocol,
            IHubContext<THub> context,
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IServiceEndpointManager serviceEndpointManager,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory,
            IEndpointRouter router,
            IServerNameProvider nameProvider,
            ServerLifetimeManager serverLifetimeManager,
            IClientResultsManager clientResultsManager,
            IClientConnectionFactory clientConnectionFactory) : base(
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
                null,
                clientResultsManager)
        {
            MockService = new ConnectionTrackingMockService();

            // just store copies of these locally to keep the base class' accessor modifiers intact
            _loggerFactory = loggerFactory;
            _clientConnectionManager = clientConnectionManager;
            _serviceProtocol = serviceProtocol;
            _clientConnectionFactory = clientConnectionFactory;
            _clientResultsManager = clientResultsManager;
        }

        internal override ServiceConnectionFactory GetServiceConnectionFactory(
            ConnectionFactory connectionFactory, ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig
            ) => new MockServiceConnectionFactory(MockService, _serviceProtocol, _clientConnectionManager, connectionFactory, _loggerFactory, connectionDelegate, _clientConnectionFactory, _nameProvider, _clientResultsManager);

        // this is the gateway for the tests to control the mock service side
        public IMockService MockService { 
            get; 
            private set; 
        }
    }
}
