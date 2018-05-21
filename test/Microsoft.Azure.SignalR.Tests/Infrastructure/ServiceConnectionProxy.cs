// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using HandshakeRequestMessage = Microsoft.Azure.SignalR.Protocol.HandshakeRequestMessage;
using HandshakeResponseMessage = Microsoft.Azure.SignalR.Protocol.HandshakeResponseMessage;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class ServiceConnectionProxy : IClientConnectionManager
    {
        private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(5);
        private static readonly IServiceProtocol _serviceProtocol = new ServiceProtocol();

        private IConnectionFactory ConnectionFactory { get; }

        public IClientConnectionManager ClientConnectionManager { get; }

        public TestConnection ConnectionContext { get; }

        private ServiceConnection ServiceConnection { get; }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections => ClientConnectionManager.ClientConnections;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>> _waitForConnectionOpen = new ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _waitForConnectionClose = new ConcurrentDictionary<string, TaskCompletionSource<object>>();

        public ServiceConnectionProxy(ConnectionDelegate callback = null)
        {
            ConnectionContext = new TestConnection();
            ConnectionFactory = new TestConnectionFactory(ConnectionContext);
            ClientConnectionManager = new ClientConnectionManager();

            ServiceConnection = new ServiceConnection(
                _serviceProtocol,
                this,
                ConnectionFactory,
                NullLoggerFactory.Instance,
                callback ?? OnConnectionAsync,
                Guid.NewGuid().ToString("N"));
        }

        public Task StartAsync()
        {
            _ = ServiceConnection.StartAsync();
            return HandshakeAsync();
        }

        public void Stop()
        {
            _ = ServiceConnection.StopAsync();
        }

        public async Task WriteMessageAsync(ServiceMessage message)
        {
            _serviceProtocol.WriteMessage(message, ConnectionContext.Application.Output);
            await ConnectionContext.Application.Output.FlushAsync();
        }

        public Task<ConnectionContext> WaitForConnectionAsync(string connectionId)
        {
            return _waitForConnectionOpen.GetOrAdd(connectionId, key => new TaskCompletionSource<ConnectionContext>()).Task;
        }

        public Task WaitForConnectionCloseAsync(string connectionId)
        {
            return _waitForConnectionClose.GetOrAdd(connectionId, key => new TaskCompletionSource<object>()).Task;
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

        public void AddClientConnection(ServiceConnectionContext clientConnection)
        {
            ClientConnectionManager.AddClientConnection(clientConnection);

            if (_waitForConnectionOpen.TryGetValue(clientConnection.ConnectionId, out var tcs))
            {
                tcs.TrySetResult(clientConnection);
            }
        }

        public void RemoveClientConnection(string connectionId)
        {
            ClientConnectionManager.RemoveClientConnection(connectionId);

            if (_waitForConnectionClose.TryGetValue(connectionId, out var tcs))
            {
                tcs.TrySetResult(null);
            }
        }

        private async Task HandshakeAsync()
        {
            using (var handshakeCts = new CancellationTokenSource(DefaultHandshakeTimeout))
            {
                await ReceiveHandshakeRequestAsync(ConnectionContext.Application.Input, handshakeCts.Token);
            }

            await WriteMessageAsync(new HandshakeResponseMessage());
        }

        private async Task ReceiveHandshakeRequestAsync(PipeReader input, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await input.ReadAsync(cancellationToken);

                var buffer = result.Buffer;
                var consumed = buffer.Start;
                var examined = buffer.End;

                try
                {
                    if (!buffer.IsEmpty)
                    {
                        if (_serviceProtocol.TryParseMessage(ref buffer, out var message))
                        {
                            consumed = buffer.Start;
                            examined = consumed;

                            if (!(message is HandshakeRequestMessage handshakeRequest))
                            {
                                throw new InvalidDataException(
                                    $"{message.GetType().Name} received when waiting for handshake request.");
                            }

                            if (handshakeRequest.Version != _serviceProtocol.Version)
                            {
                                throw new InvalidDataException("Protocol version not supported.");
                            }

                            break;
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // Not enough data, and we won't be getting any more data.
                        throw new InvalidOperationException(
                            "Service connectioned disconnected before sending a handshake request");
                    }
                }
                finally
                {
                    input.AdvanceTo(consumed, examined);
                }
            }
        }
    }
}
