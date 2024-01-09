// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class ServiceConnectionProxy : IClientConnectionManager, IClientConnectionFactory, IServiceConnectionFactory
    {
        private static readonly IServiceProtocol SharedServiceProtocol = new ServiceProtocol();
        private readonly PipeOptions _clientPipeOptions;

        public TestConnectionFactory ConnectionFactory { get; }

        public IClientConnectionManager ClientConnectionManager { get; }

        public IClientInvocationManager ClientInvocationManager { get; }

        public IServiceConnectionContainer ServiceConnectionContainer { get; }

        public IServiceMessageHandler ServiceMessageHandler { get; }

        public IServerNameProvider ServerNameProvider { get; }

        public ConnectionDelegate ConnectionDelegateCallback { get; }

        public ConcurrentDictionary<string, TestConnection> ConnectionContexts { get; } =
            new ConcurrentDictionary<string, TestConnection>();

        public ConcurrentDictionary<string, ServiceConnection> ServiceConnections { get; } = new ConcurrentDictionary<string, ServiceConnection>();

        public IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections => ClientConnectionManager.ClientConnections;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>> _waitForConnectionOpen = new ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _waitForConnectionClose = new ConcurrentDictionary<string, TaskCompletionSource<object>>();
        private readonly ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>> _waitForApplicationMessage = new ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource<ConnectionContext>> _waitForServerConnection = new ConcurrentDictionary<int, TaskCompletionSource<ConnectionContext>>();
        private int _connectedServerConnectionCount;

        public ServiceConnectionProxy(
            ConnectionDelegate callback = null,
            PipeOptions clientPipeOptions = null,
            Func<Func<TestConnection, Task>, TestConnectionFactory> connectionFactoryCallback = null,
            int connectionCount = 1)
        {
            ConnectionFactory = connectionFactoryCallback?.Invoke(ConnectionFactoryCallbackAsync) ?? new TestConnectionFactory(ConnectionFactoryCallbackAsync);
            ClientConnectionManager = new ClientConnectionManager();
            
            ClientInvocationManager = new DefaultClientInvocationManager();
            _clientPipeOptions = clientPipeOptions;
            ConnectionDelegateCallback = callback ?? OnConnectionAsync;

            ServerNameProvider = new DefaultServerNameProvider();

            // these two lines should be located in the end of this constructor.
            ServiceConnectionContainer = new StrongServiceConnectionContainer(this, connectionCount, null, new TestHubServiceEndpoint(), NullLogger.Instance);
            ServiceMessageHandler = (StrongServiceConnectionContainer) ServiceConnectionContainer;
        }

        public IServiceConnection Create(HubServiceEndpoint endpoint,
                                         IServiceMessageHandler serviceMessageHandler,
                                         AckHandler ackHandler,
                                         ServiceConnectionType type)
        {
            var connectionId = Guid.NewGuid().ToString("N");
            var connection = new ServiceConnection(
                SharedServiceProtocol,
                this,
                ConnectionFactory,
                NullLoggerFactory.Instance,
                ConnectionDelegateCallback,
                this,
                ServerNameProvider.GetName(),
                connectionId,
                endpoint,
                serviceMessageHandler,
                null,
                ClientInvocationManager,
                ackHandler,
                type);
            ServiceConnections.TryAdd(connectionId, connection);
            return connection;
        }

        public bool NewConnectionsCreationPaused { get; set; }

        private async Task ConnectionFactoryCallbackAsync(TestConnection connection)
        {
            while (NewConnectionsCreationPaused)
            {
                await Task.Delay(10);
            }
            ConnectionContexts.TryAdd(connection.ConnectionId, connection);
            // Start a process for each server connection
            _ = StartProcessApplicationMessagesAsync(connection);
        }

        private async Task StartProcessApplicationMessagesAsync(TestConnection connection)
        {
            await ServiceConnections[connection.ConnectionId].ConnectionInitializedTask;

            if (ServiceConnections[connection.ConnectionId].Status == ServiceConnectionStatus.Connected)
            {
                Interlocked.Increment(ref _connectedServerConnectionCount);
                if (_waitForServerConnection.TryGetValue(_connectedServerConnectionCount, out var tcs))
                {
                    tcs.TrySetResult(connection);
                }
                await ProcessApplicationMessagesAsync(connection.Application.Input);
            } 
        }

        public Task StartAsync()
        {
            return ServiceConnectionContainer.StartAsync();
        }

        public Task WaitForServerConnectionsInited()
        {
            return Task.WhenAll(ServiceConnections.Values.Select(s => s.ConnectionInitializedTask));
        }

        public async Task ProcessApplicationMessagesAsync(PipeReader pipeReader)
        {
            try
            {
                while (true)
                {
                    var result = await pipeReader.ReadAsync();
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
                            if (SharedServiceProtocol.TryParseMessage(ref buffer, out var message))
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
                        pipeReader.AdvanceTo(consumed, examined);
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
            _ = Task.WhenAll(ServiceConnections.Select(c => c.Value.StopAsync()));
            foreach (var connectionContext in ConnectionContexts)
            {
                connectionContext.Value.Application.Input.CancelPendingRead();
            }
        }

        /// <summary>
        /// Write a message to server connection.
        /// </summary>
        public async Task WriteMessageAsync(TestConnection context, ServiceMessage message)
        {
            SharedServiceProtocol.WriteMessage(message, context.Application.Output);
            await context.Application.Output.FlushAsync();
        }

        /// <summary>
        /// Write a message to the first connected server connection
        /// </summary>
        public async Task WriteMessageAsync(ServiceMessage message)
        {
            foreach (var connection in ServiceConnections)
            {
                if (connection.Value.Status == ServiceConnectionStatus.Connecting)
                {
                    await connection.Value.ConnectionInitializedTask;
                }

                if (connection.Value.Status == ServiceConnectionStatus.Connected)
                {
                    var context = ConnectionContexts[connection.Key];
                    SharedServiceProtocol.WriteMessage(message, context.Application.Output);
                    await context.Application.Output.FlushAsync();
                    return;
                }
            }
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
            return _waitForServerConnection.GetOrAdd(count, key => new TaskCompletionSource<ConnectionContext>()).Task;
        }

        private Task OnConnectionAsync(ConnectionContext connection)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            connection.ConnectionClosed.Register(() => tcs.TrySetResult(null));

            return tcs.Task;
        }

        public bool TryAddClientConnection(ClientConnectionContext connection)
        {
            if (ClientConnectionManager.TryAddClientConnection(connection))
            {
                if (_waitForConnectionOpen.TryGetValue(connection.ConnectionId, out var tcs))
                {
                    tcs.TrySetResult(connection);
                }
                return true;
            }
            return false;
        }

        public bool TryRemoveClientConnection(string connectionId, out ClientConnectionContext connection)
        {
            if (ClientConnectionManager.TryRemoveClientConnection(connectionId, out connection))
            {
                if (_waitForConnectionClose.TryGetValue(connectionId, out var tcs))
                {
                    tcs.TrySetResult(null);
                }
                return true;
            }
            return false;
        }

        public ClientConnectionContext CreateConnection(OpenConnectionMessage message, Action<HttpContext> configureContext = null)
        {
            return new ClientConnectionContext(message, configureContext, _clientPipeOptions, _clientPipeOptions);
        }

        private void AddApplicationMessage(Type type, ServiceMessage message)
        {
            if (_waitForApplicationMessage.TryRemove(type, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }

        public Task WhenAllCompleted()
        {
            return Task.CompletedTask;
        }
    }
}
