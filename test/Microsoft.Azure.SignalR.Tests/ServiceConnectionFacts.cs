// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Infrastructure;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceConnectionFacts
    {
        private static readonly ServiceProtocol Protocol = new ServiceProtocol();

        [Fact]
        public async Task ServiceConnectionStartsConnection()
        {
            var connectionId1 = Guid.NewGuid().ToString("N");
            var connectionId2 = Guid.NewGuid().ToString("N");

            var proxy = new ServiceConnectionProxy();

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ =  proxy.StartAsync();
            await serverTask.OrTimeout();


            Assert.Empty(proxy.ClientConnectionManager.ClientConnections);

            // Wait for the connection to appear, we need to do this before
            // sending the message to avoid races
            var connection1Task = proxy.WaitForConnectionAsync(connectionId1);

            // Create a new client connection
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId1, null));

            var connection1 = await connection1Task.OrTimeout();

            Assert.Single(proxy.ClientConnectionManager.ClientConnections);

            // Wait for the connection to appear
            var connectionTask2 = proxy.WaitForConnectionAsync(connectionId2);

            // Create another client connection
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId2, null));

            var connection2 = await connectionTask2.OrTimeout();

            Assert.Equal(2, proxy.ClientConnectionManager.ClientConnections.Count);

            // Send a message to client 1
            await proxy.WriteMessageAsync(new ConnectionDataMessage(connectionId1, Encoding.ASCII.GetBytes("Hello")));

            var item = await connection1.Transport.Input.ReadSingleAsync().OrTimeout();

            Assert.Equal("Hello", Encoding.ASCII.GetString(item));

            var connection1CloseTask = proxy.WaitForConnectionCloseAsync(connectionId1);

            // Close client 1
            await proxy.WriteMessageAsync(new CloseConnectionMessage(connectionId1, null));

            await connection1CloseTask.OrTimeout();

            Assert.Single(proxy.ClientConnectionManager.ClientConnections);

            // Close client 2
            var connection2CloseTask = proxy.WaitForConnectionCloseAsync(connectionId2);

            await proxy.WriteMessageAsync(new CloseConnectionMessage(connectionId2, null));

            await connection2CloseTask.OrTimeout();

            Assert.Empty(proxy.ClientConnectionManager.ClientConnections);

            proxy.Stop();
        }

        [Fact]
        public async Task ClosingConnectionSendsCloseMessage()
        {
            var proxy = new ServiceConnectionProxy(context =>
            {
                // Just let the connection end immediately
                return Task.CompletedTask;
            });

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var task = proxy.WaitForConnectionAsync("1");
            
            await proxy.WriteMessageAsync(new OpenConnectionMessage("1", null));

            var connection = await task.OrTimeout();

            var message = await ReadServiceMessageAsync<CloseConnectionMessage>(proxy.ConnectionContext.Application.Input);
            Assert.Equal(message.ConnectionId, connection.ConnectionId);

            proxy.Stop();
        }

        [Fact]
        public async Task WritingMessagesFromConnectionGetsSentAsConnectionData()
        {
            var proxy = new ServiceConnectionProxy();

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var task = proxy.WaitForConnectionAsync("1");
            
            await proxy.WriteMessageAsync(new OpenConnectionMessage("1", null));

            var connection = await task.OrTimeout();

            await connection.Transport.Output.WriteAsync(Encoding.ASCII.GetBytes("Hello World"));

            var message = await ReadServiceMessageAsync<ConnectionDataMessage>(proxy.ConnectionContext.Application.Input);
            Assert.Equal(message.ConnectionId, connection.ConnectionId);
            Assert.Equal("Hello World", Encoding.ASCII.GetString(message.Payload.ToArray()));

            proxy.Stop();
        }

        [Fact]
        public async Task WritingMultiSegmentMessageConnectionMessageWritesSingleMessage()
        {
            // 10 byte segment size
            var clientPipeOptions = new PipeOptions(minimumSegmentSize: 10);
            var proxy = new ServiceConnectionProxy(clientPipeOptions: clientPipeOptions);

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var task = proxy.WaitForConnectionAsync("1");

            await proxy.WriteMessageAsync(new OpenConnectionMessage("1", null));

            var connection = await task.OrTimeout();
            var outputMessage = "This message should be more than 10 bytes";

            await connection.Transport.Output.WriteAsync(Encoding.ASCII.GetBytes(outputMessage));

            var message = await ReadServiceMessageAsync<ConnectionDataMessage>(proxy.ConnectionContext.Application.Input);
            Assert.Equal(message.ConnectionId, connection.ConnectionId);
            Assert.Equal(outputMessage, Encoding.ASCII.GetString(message.Payload.ToArray()));

            proxy.Stop();
        }

        [Fact]
        public async Task ServiceConnectionSendsPingMessage()
        {
            var proxy = new ServiceConnectionProxy();

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            // Check more than once since it happens periodically
            for (int i = 0; i < 2; i++)
            {
                await ReadServiceMessageAsync<PingMessage>(proxy.ConnectionContext.Application.Input, 6000);
            }

            proxy.Stop();
        }

        // Test when ConnectAsync throws Exception
        [Fact]
        public async Task ServiceReconnectWhenConnectionAsyncThrowException()
        {
            var connectionContext = new TestConnection();
            var connectionFactory = new ConnectionFactoryForReconnection(connectionContext);

            var proxy = new ServiceConnectionProxy(connectionContext: connectionContext,
                connectionFactory: connectionFactory);
            ((ConnectionFactoryForReconnection)proxy.ConnectionFactory).SetProxy(proxy);

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var connectionId = Guid.NewGuid().ToString("N");

            var connectionTask = proxy.WaitForConnectionAsync(connectionId);
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId, null));
            await connectionTask.OrTimeout();
        }

        //Test when Handshack return ErrorMessage
        [Fact]
        public async Task ServiceReconnectWhenHandshackErrorMessage()
        {
            var proxy = new ServiceConnectionProxy(handshackMessageFactory: new TestHandshackMessageFactory("Got Error"));

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var connectionId = Guid.NewGuid().ToString("N");
            var connectionTask = proxy.WaitForConnectionAsync(connectionId);

            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId, null));
            
            // Connection exits so the Task should be timeout
            Assert.False(Task.WaitAll(new[] {connectionTask}, TimeSpan.FromSeconds(1)));
        }

        //Test when Handshack throws Exception
        [Fact]
        public async Task ServiceReconnectWhenHandshackThrowException()
        {
            var proxy = new ServiceConnectionProxy(handshackMessageFactory: new HandshackMessageFactoryForReconnection());

            // Throw exception for 3 times and succeed in the 4th retry
            var serverTask = proxy.WaitForServerConnectionAsync(4);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var connectionId = Guid.NewGuid().ToString("N");

            var connectionTask = proxy.WaitForConnectionAsync(connectionId);
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId, null));
            await connectionTask.OrTimeout();
        }

        // Test when Connection throws Exception
        [Fact]
        public async Task ServiceReconnectWhenConnectionThrowException()
        {
            var connection = new TestConnection();
            var proxy = new ServiceConnectionProxy(connectionContext: connection);

            var serverTask1 = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask1.OrTimeout();

            // Try to wait the second handshack after reconnection
            var serverTask2 = proxy.WaitForServerConnectionAsync(2);

            // Dispose the connection and send one message, so that it will throw exception in ServiceConnection
            connection.Dispose();
            var connectionId1 = Guid.NewGuid().ToString("N");
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId1, null));

            await serverTask2.OrTimeout();

            // Verify the server connection works well
            var connectionId2 = Guid.NewGuid().ToString("N");
            var connectionTask = proxy.WaitForConnectionAsync(connectionId2);
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId2, null));

            await connectionTask.OrTimeout();
        }

        private async Task<T> ReadServiceMessageAsync<T>(PipeReader input, int timeout = 5000) where T : ServiceMessage
        {
            var data = await input.ReadSingleAsync().OrTimeout(timeout);
            var buffer = new ReadOnlySequence<byte>(data);
            Assert.True(Protocol.TryParseMessage(ref buffer, out var message));
            return Assert.IsType<T>(message);
        }

        private class ConnectionFactoryForReconnection : IConnectionFactory
        {
            private readonly ConnectionContext _connection;

            private const int RestartCountMax = 3;

            private int _currentRestartCount = 0;

            private ServiceConnectionProxy _proxy;

            public ConnectionFactoryForReconnection(ConnectionContext connection)
            {
                _connection = connection;
            }

            public void SetProxy(ServiceConnectionProxy proxy)
            {
                _proxy = proxy;
            }

            public Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, CancellationToken cancellationToken = default)
            {
                // Throw exception to test reconnection
                if (_currentRestartCount < RestartCountMax)
                {
                    _currentRestartCount = _currentRestartCount + 1;
                    throw new Exception();
                }

                _ = _proxy.HandshakeAsync();
                return Task.FromResult(_connection);
            }

            public Task DisposeAsync(ConnectionContext connection)
            {
                return Task.CompletedTask;
            }
        }

        private class HandshackMessageFactoryForReconnection : IHandshackMessageFactory
        {
            private const int RestartCountMax = 3;

            private int _currentRestartCount = 0;

            public ServiceMessage GetHandshackResposeMessage()
            {
                if (_currentRestartCount < RestartCountMax)
                {
                    _currentRestartCount = _currentRestartCount + 1;
                    return new BroadcastDataMessage(null);
                }

                return new HandshakeResponseMessage();
            }
        }
    }
}
