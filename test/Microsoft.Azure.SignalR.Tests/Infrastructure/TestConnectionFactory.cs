// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestConnectionFactory : IConnectionFactory
    {
        private readonly Func<TestConnection, Task> _connectCallback;

        private int _connectionCount;

        private readonly ConcurrentDictionary<int, TaskCompletionSource<ConnectionContext>> _waitForConnection =
            new ConcurrentDictionary<int, TaskCompletionSource<ConnectionContext>>();

        public virtual TestConnection CurrentConnectionContext { get; private set; }

        public List<DateTime> Times { get; } = new List<DateTime>();

        public TestConnectionFactory(Func<TestConnection, Task> connectCallback = null)
        {
            _connectCallback = connectCallback;
        }
        
        public async Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId,
            CancellationToken cancellationToken = default)
        {
            Times.Add(DateTime.Now);
            CurrentConnectionContext = null;

            var connection = new TestConnection();
            // Start a task to process handshake request from the newly-created server connection.
            _ = HandshakeAsync(connection);

            if (_connectCallback != null)
            {
                await _connectCallback(connection);
            }

            CurrentConnectionContext = connection;
            return connection;
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        private async Task HandshakeAsync(TestConnection connection)
        {
            await DoHandshakeAsync(connection);
            AddConnection(connection);
        }

        /// <summary>
        /// Allow sub-class to override the handshake behavior
        /// </summary>
        protected virtual async Task DoHandshakeAsync(TestConnection connection)
        {
            await HandshakeUtils.ReceiveHandshakeRequestAsync(connection.Application.Input);
            await HandshakeUtils.SendHandshakeResponseAsync(connection.Application.Output);
        }

        public Task<ConnectionContext> WaitForConnectionAsync(int connectionCount)
        {
            return _waitForConnection
                .GetOrAdd(connectionCount, key => new TaskCompletionSource<ConnectionContext>()).Task;
        }

        private void AddConnection(ConnectionContext connection)
        {
            var count = Interlocked.Increment(ref _connectionCount);

            if (_waitForConnection.TryGetValue(count, out var tcs))
            {
                tcs.TrySetResult(connection);
            }
        }
    }
}
