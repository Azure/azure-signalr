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
        private readonly Func<TestConnectionContext, Task> _connectCallback;

        public List<DateTime> Times { get; } = new List<DateTime>();

        public TestConnectionFactory(Func<TestConnectionContext, Task> connectCallback)
        {
            _connectCallback = connectCallback;
        }
        
        public async Task<ConnectionContext> ConnectAsync(HubServiceEndpoint endpoint, TransferFormat transferFormat, string connectionId, string target,
            CancellationToken cancellationToken = default, IDictionary<string, string> headers = null)
        {
            Times.Add(DateTime.Now);

            var connection = new TestConnectionContext
            {
                ConnectionId = connectionId,
                Target = target
            };
            // Start a task to process handshake request from the newly-created server connection.
            _ = HandshakeAsync(connection);

            // Do something for test purpose
            await AfterConnectedAsync(connection);

            await _connectCallback(connection);

            return connection;
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        private async Task HandshakeAsync(TestConnectionContext connectionContext)
        {
            await DoHandshakeAsync(connectionContext);
        }

        /// <summary>
        /// Allow sub-class to override the handshake behavior
        /// </summary>
        protected virtual async Task DoHandshakeAsync(TestConnectionContext connectionContext)
        {
            await HandshakeUtils.ReceiveHandshakeRequestAsync(connectionContext.Application.Input);
            await HandshakeUtils.SendHandshakeResponseAsync(connectionContext.Application.Output);
        }

        /// <summary>
        /// Allow sub-class to override. Do something after connect being created.
        /// </summary>
        protected virtual Task AfterConnectedAsync(TestConnectionContext connectionContext)
        {
            return Task.CompletedTask;
        }
    }
}
