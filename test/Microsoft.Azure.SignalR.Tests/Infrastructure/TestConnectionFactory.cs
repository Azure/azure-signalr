// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR.Tests
{
    public class TestConnectionFactory : IConnectionFactory
    {
        private readonly ConnectionContext _connection;

        public TestConnectionFactory(ConnectionContext connection)
        {
            _connection = connection;
        }

        public Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_connection);
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }
    }
}
