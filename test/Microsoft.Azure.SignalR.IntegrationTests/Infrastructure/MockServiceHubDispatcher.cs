// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal class MockServiceHubDispatcher<THub> : ServiceHubDispatcher<THub> where THub : Hub
    {
        public MockServiceHubDispatcher(
            IServiceProtocol serviceProtocol,
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IServiceEndpointManager serviceEndpointManager,
            IOptions<ServiceOptions> options,
            ILoggerFactory loggerFactory,
            IEndpointRouter router,
            IServerNameProvider nameProvider,
            ServerLifetimeManager serverLifetimeManager,
            IClientConnectionFactory clientConnectionFactory) : base(
                serviceProtocol,
                serviceConnectionManager,
                clientConnectionManager,
                serviceEndpointManager,
                options,
                loggerFactory,
                router,
                nameProvider,
                serverLifetimeManager,
                clientConnectionFactory)
        {
            MockService = new MockServiceMock();
        }

        internal override ServiceConnectionFactory GetServiceConnectionFactory(
            ConnectionFactory connectionFactory, ConnectionDelegate connectionDelegate, Action<HttpContext> contextConfig
            ) => new MockServiceConnectionFactory(MockService, _serviceProtocol, _clientConnectionManager, connectionFactory, _loggerFactory, connectionDelegate, _clientConnectionFactory, _nameProvider);

        // this is the gateway for the tests to control the mock service side
        public IMockService MockService { get; private set; }
    }
}
