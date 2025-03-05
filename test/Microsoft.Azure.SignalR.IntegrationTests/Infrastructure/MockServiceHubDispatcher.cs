// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;

internal sealed class MockServiceHubDispatcher<THub>(
    IMockService mockService,
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
    IConnectionFactory connectionFactory,
    IServiceProvider serviceProvider,
    IHubProtocolResolver hubProtocolResolver) : ServiceHubDispatcher<THub>(
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
        connectionFactory,
        serviceProvider,
        null) where THub : Hub
{
    internal override ServiceConnectionFactory GetServiceConnectionFactory(ConnectionDelegate connectionDelegate)
    {
        return ActivatorUtilities.CreateInstance<MockServiceConnectionFactory>(serviceProvider, connectionDelegate);
    }

    // this is the gateway for the tests to control the mock service side
    public IMockService MockService { get; private set; } = mockService;
}
