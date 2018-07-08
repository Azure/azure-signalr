// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class ServiceConnectionProxy : IClientConnectionManager, IClientConnectionFactory
    {
        private static readonly IServiceProtocol ServiceProtocol = new ServiceProtocol();
        private readonly PipeOptions _clientPipeOptions;

        public TestConnectionFactory ConnectionFactory { get; }

        public IClientConnectionManager ClientConnectionManager { get; }

        public TestConnection ConnectionContext => ConnectionFactory.CurrentConnectionContext;

        public ServiceConnection ServiceConnection { get; }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections => ClientConnectionManager.ClientConnections;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>> _waitForConnectionOpen = new ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _waitForConnectionClose = new ConcurrentDictionary<string, TaskCompletionSource<object>>();
        private readonly ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>> _waitForApplicationMessage = new ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>>();

        public ServiceConnectionProxy(ConnectionDelegate callback = null, PipeOptions clientPipeOptions = null,
            TestConnectionFactory connectionFactory = null)
        {
            ConnectionFactory = connectionFactory ?? new TestConnectionFactory();
            ClientConnectionManager = new ClientConnectionManager();
            _clientPipeOptions = clientPipeOptions;

            ServiceConnection = new ServiceConnection(
                ServiceProtocol,
                this,
                ConnectionFactory,
                NullLoggerFactory.Instance,
                callback ?? OnConnectionAsync,
                this,
                Guid.NewGuid().ToString("N"));
        }

        public Task StartAsync()
        {
            return ServiceConnection.StartAsync();
        }

        public async Task ProcessApplicationMessagesAsync()
        {
            try
            {
                while (true)
                {
                    var result = await ConnectionContext.Application.Input.ReadAsync();
                    var buffer = result.Buffer;

                    var consumed = buffer.Start;
                    var examined = buffer.End;

                    try
                    {
                        if (result.IsCanceled)
                        {
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            if (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                            {
                                consumed = buffer.Start;
                                examined = consumed;

                                AddApplicationMessage(message.GetType(), message);
                            }
                        }

                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        ConnectionContext.Application.Input.AdvanceTo(consumed, examined);
                    }
                }
            }
            catch
            {
                // Ignored.
            }
        }

        public void Stop()
        {
            _ = ServiceConnection.StopAsync();
        }

        public async Task WriteMessageAsync(ServiceMessage message)
        {
            ServiceProtocol.WriteMessage(message, ConnectionContext.Application.Output);
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

        public Task<ServiceMessage> WaitForApplicationMessageAsync(Type type)
        {
            return _waitForApplicationMessage.GetOrAdd(type, key => new TaskCompletionSource<ServiceMessage>()).Task;
        }

        public Task<ConnectionContext> WaitForServerConnectionAsync(int count)
        {
            return ConnectionFactory.WaitForConnectionAsync(count);
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

        public ServiceConnectionContext CreateConnection(OpenConnectionMessage message)
        {
            return new ServiceConnectionContext(message, _clientPipeOptions, _clientPipeOptions);
        }

        private void AddApplicationMessage(Type type, ServiceMessage message)
        {
            if (_waitForApplicationMessage.TryGetValue(type, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }
    }
}
