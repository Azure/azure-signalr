// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal sealed class TestConnectionFactory : IConnectionFactory
    {
        private readonly ConnectionDelegate _connectCallback;
        private TaskCompletionSource<TestConnectionContext> _waitForServerConnection = new TaskCompletionSource<TestConnectionContext>();

        public TestConnectionFactory(ConnectionDelegate connectCallback = null)
        {
            _connectCallback = connectCallback ?? OnConnectionAsync;
        }

        public Task<TestConnectionContext> GetConnectedServerAsync()
        {
            return _waitForServerConnection.Task;
        }

        public Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken = default, IDictionary<string, string> headers = null)
        {
            var connection = new TestConnectionContext();
            _connectCallback?.Invoke(connection);

            _waitForServerConnection.TrySetResult(connection);
            return Task.FromResult<ConnectionContext>(connection);
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        private Task OnConnectionAsync(ConnectionContext connection)
        {
            var tcs = new TaskCompletionSource<object>();

            // Wait for the connection to close
            connection.Transport.Input.OnWriterCompleted((ex, state) =>
            {
                tcs.TrySetResult(null);
            },
            null);

            return tcs.Task;
        }
    }
}
