// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestConnectionFactory : IConnectionFactory
    {
        private readonly Func<TestConnection, Task> _connectCallback;

        public IList<TestConnection> Connections = new List<TestConnection>();

        public List<DateTime> Times { get; } = new List<DateTime>();

        public TestConnectionFactory()
        {
            _connectCallback = null;
        }

        public TestConnectionFactory(Func<TestConnection, Task> connectCallback)
        {
            _connectCallback = connectCallback;
        }

        public HandshakeRequestMessage HandshakeRequest { get; set; }

        public async Task<ConnectionContext> ConnectAsync(HubServiceEndpoint endpoint,
                                                          TransferFormat transferFormat,
                                                          string connectionId,
                                                          string target,
                                                          CancellationToken cancellationToken = default,
                                                          IDictionary<string, string> headers = null)
        {
            Times.Add(DateTime.Now);

            var connection = new TestConnection
            {
                ConnectionId = connectionId,
                Target = target
            };
            Connections.Add(connection);

            // Start a task to process handshake request from the newly-created server connection.
            _ = HandshakeAsync(connection);

            // Do something for test purpose
            await AfterConnectedAsync(connection);

            if (_connectCallback != null)
            {
                await _connectCallback(connection);
            }

            return connection;
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        private async Task HandshakeAsync(TestConnection connection)
        {
            await DoHandshakeAsync(connection);
        }

        /// <summary>
        /// Allow sub-class to override the handshake behavior
        /// </summary>
        protected virtual async Task DoHandshakeAsync(TestConnection connection)
        {
            HandshakeRequest = await HandshakeUtils.ReceiveHandshakeRequestAsync(connection.Application.Input);
            await HandshakeUtils.SendHandshakeResponseAsync(connection.Application.Output);
        }

        /// <summary>
        /// Allow sub-class to override. Do something after connect being created.
        /// </summary>
        protected virtual Task AfterConnectedAsync(TestConnection connection)
        {
            return Task.CompletedTask;
        }
    }
}
