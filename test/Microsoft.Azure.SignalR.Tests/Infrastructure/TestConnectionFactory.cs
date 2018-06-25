// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestConnectionFactory : IConnectionFactory
    {
        private readonly ConnectionContext _connection;
        private readonly ServiceConnectionProxy _proxy;

        public TestConnectionFactory(ConnectionContext connection, ServiceConnectionProxy proxy)
        {
            _connection = connection;
            _proxy = proxy;
        }

        public Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, CancellationToken cancellationToken = default)
        {
            _ =  _proxy.HandshakeAsync();
            return Task.FromResult(_connection);
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }
    }
}
