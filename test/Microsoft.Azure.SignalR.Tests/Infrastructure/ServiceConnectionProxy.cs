// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class ServiceConnectionProxy : IClientConnectionManager, IClientConnectionFactory
    {
        private static readonly IServiceProtocol SharedServiceProtocol = new ServiceProtocol();
        private readonly PipeOptions _clientPipeOptions;

        public TestConnectionFactory ConnectionFactory { get; }

        public IClientConnectionManager ClientConnectionManager { get; }

        public ConcurrentDictionary<string, TestConnection> ConnectionContexts { get; } =
            new ConcurrentDictionary<string, TestConnection>();

        public IServiceConnectionContainer ServiceConnectionContainer { get; }

        public ConcurrentDictionary<string, ServiceConnection> ServiceConnections { get; } = new ConcurrentDictionary<string, ServiceConnection>();

        public IServiceMessageHandler ServiceMessageHandler { get; }

        public ConnectionDelegate ConnectionDelegateCallback { get; }

        public Func<TestConnection, Task> ConnectionFactoryCallback { get; }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections => ClientConnectionManager.ClientConnections;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>> _waitForConnectionOpen = new ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _waitForConnectionClose = new ConcurrentDictionary<string, TaskCompletionSource<object>>();
        private readonly ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>> _waitForApplicationMessage = new ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>>();

        //public ServiceConnectionProxy(ConnectionDelegate callback = null, PipeOptions clientPipeOptions = null,
        //    TestConnectionFactory connectionFactory = null, IServiceMessageHandler serviceMessageHandler = null, int connectionCount = 1)
        //{
        //    ConnectionFactory = connectionFactory ?? new TestConnectionFactory(ConnectionFactoryCallbackAsync);
        //    ClientConnectionManager = new ClientConnectionManager();
        //    _clientPipeOptions = clientPipeOptions;
        //    ConnectionDelegateCallback = callback ?? OnConnectionAsync;

        //    var connectionContainer = new StrongServiceConnectionContainer(new TestServiceConnectionFactory(CreateServiceConnection), ConnectionFactory, connectionCount, new ServiceEndpoint("", ""));
        //    ServiceMessageHandler = serviceMessageHandler ?? connectionContainer;
        //    ServiceConnectionContainer = connectionContainer;
        //}

        public ServiceConnectionProxy(ConnectionDelegate callback = null, PipeOptions clientPipeOptions = null,
            Type connectionFactoryType = null, Func<TestConnection, Task> connectionFactoryCallback = null, IServiceMessageHandler serviceMessageHandler = null, int connectionCount = 1)
        {
            Type factoryType = connectionFactoryType ?? typeof(TestConnectionFactory);
            ConnectionFactoryCallback = connectionFactoryCallback;
            Func<TestConnection, Task> callbackParam = ConnectionFactoryCallbackAsync;

            ConnectionFactory = (TestConnectionFactory) Activator.CreateInstance(factoryType, callbackParam);
            //ConnectionFactory = connectionFactory ?? new TestConnectionFactory(ConnectionFactoryCallbackAsync);
            ClientConnectionManager = new ClientConnectionManager();
            _clientPipeOptions = clientPipeOptions;
            ConnectionDelegateCallback = callback ?? OnConnectionAsync;

            var connectionContainer = new StrongServiceConnectionContainer(new TestServiceConnectionFactory(CreateServiceConnection), ConnectionFactory, connectionCount, new ServiceEndpoint("", ""));
            ServiceMessageHandler = serviceMessageHandler ?? connectionContainer;
            ServiceConnectionContainer = connectionContainer;
        }

        private ServiceConnection CreateServiceConnection(ServerConnectionType type)
        {
            var connectionId = Guid.NewGuid().ToString("N");
            var connection = new ServiceConnection(
                SharedServiceProtocol,
                this,
                ConnectionFactory,
                NullLoggerFactory.Instance,
                ConnectionDelegateCallback,
                this,
                connectionId,
                ServiceMessageHandler,
                type);
            ServiceConnections.TryAdd(connectionId, connection);
            return connection;
        }

        private async Task ConnectionFactoryCallbackAsync(TestConnection connection)
        {
            if (ConnectionFactoryCallback != null)
            {
                await ConnectionFactoryCallback(connection);
            }
            ConnectionContexts.TryAdd(connection.ConnectionId, connection);
            // Start a process for each server connection
            _ = StartProcessApplicationMessagesAsync(connection);
        }

        private async Task StartProcessApplicationMessagesAsync(TestConnection connection)
        {
            await connection.ConnectionInitialized;
            await ProcessApplicationMessagesAsync(connection.Application.Input);
        }

        public Task StartAsync()
        {
            return ServiceConnectionContainer.StartAsync();
        }

        public bool IsConnected
        {
            get
            {
                bool isConnected = true;
                foreach (var connection in ServiceConnections)
                {
                    isConnected = isConnected && connection.Value.IsConnected;
                }

                return isConnected;
            }
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
            //_ = ServiceConnection.StopAsync();
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
        /// Write a message to the first server connection
        /// </summary>
        public async Task WriteMessageAsync(ServiceMessage message)
        {
            var context = ConnectionContexts.First().Value;
            SharedServiceProtocol.WriteMessage(message, context.Application.Output);
            await context.Application.Output.FlushAsync();
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
            if (_waitForApplicationMessage.TryRemove(type, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }
    }
}
