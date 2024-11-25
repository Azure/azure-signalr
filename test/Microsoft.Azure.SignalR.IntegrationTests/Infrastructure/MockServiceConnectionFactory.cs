// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;

internal class MockServiceConnectionFactory : ServiceConnectionFactory
{
    private IMockService _mockService;

    public MockServiceConnectionFactory(
        IMockService mockService,
        IServiceProtocol serviceProtocol,
        IClientConnectionManager clientConnectionManager,
        IConnectionFactory connectionFactory,
        ILoggerFactory loggerFactory,
        ConnectionDelegate connectionDelegate,
        IClientConnectionFactory clientConnectionFactory,
        IClientInvocationManager clientInvocationManager,
        IServerNameProvider nameProvider,
        IHubProtocolResolver hubProtocolResolver)
        : base(
              serviceProtocol,
              clientConnectionManager,
              connectionFactory,
              loggerFactory,
              connectionDelegate,
              clientConnectionFactory,
              nameProvider,
              null,
              clientInvocationManager,
              hubProtocolResolver)
    {
        _mockService = mockService;
    }

    public override IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, AckHandler ackHandler, ServiceConnectionType type)
    {
        var serviceConnection = base.Create(endpoint, serviceMessageHandler, ackHandler, type);
        return new MockServiceConnection(_mockService, serviceConnection);
    }
}
