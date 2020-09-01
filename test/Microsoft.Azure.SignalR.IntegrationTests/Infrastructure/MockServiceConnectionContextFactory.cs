// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal class MockServiceConnectionContextFactory : IConnectionFactory
    {
        IMockService _mockService;
        public MockServiceConnectionContextFactory(IMockService mockService)
        {
            _mockService = mockService;
        }

        public Task<ConnectionContext> ConnectAsync(HubServiceEndpoint endpoint, TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken = default, IDictionary<string, string> headers = null)
        {
            // ConnectAsync merely means establish a physical connection. 
            // In our case this means connect the pipes and start the message processing loops
            ConnectionContext c = new MockServiceConnectionContext(_mockService, endpoint, target, connectionId);

            return Task.FromResult(c);
        }

        public async Task DisposeAsync(ConnectionContext connection)
        {
            await ((MockServiceConnectionContext)connection).DisposeAsync();
        }
    }
}
