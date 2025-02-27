// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;

#nullable enable

internal class MockServiceConnectionContextFactory(IMockService mockService) : IConnectionFactory
{
    public Task<ConnectionContext> ConnectAsync(HubServiceEndpoint endpoint,
                                                TransferFormat transferFormat,
                                                string connectionId,
                                                string target,
                                                CancellationToken cancellationToken = default)
    {
        // ConnectAsync merely means establish a physical connection.
        // In our case this means connect the pipes and start the message processing loops
        ConnectionContext c = new MockServiceConnectionContext(mockService, endpoint, target, connectionId);
        return Task.FromResult(c);
    }

    public async Task DisposeAsync(ConnectionContext connection)
    {
        await ((MockServiceConnectionContext)connection).DisposeAsync();
    }
}
