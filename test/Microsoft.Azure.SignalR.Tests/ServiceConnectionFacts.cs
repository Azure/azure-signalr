// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;
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

            var httpContext1 = connection1.GetHttpContext();
            Assert.NotNull(httpContext1);
            Assert.Empty(httpContext1.Request.Headers);
            Assert.Empty(httpContext1.Request.Query);
            Assert.Equal(string.Empty, httpContext1.Request.Path);

            // Wait for the connection to appear
            var connectionTask2 = proxy.WaitForConnectionAsync(connectionId2);

            // Create another client connection
            const string headerKey1 = "custom-header-1";
            const string headerValue1 = "custom-value-1";
            const string headerKey2 = "custom-header-2";
            var headerValue2 = new[] {"custom-value-2a", "custom-value-2b"};
            const string headerKey3 = "custom-header-3";
            var headerValue3 = new[] {"custom-value-3a", "custom-value-3b", "custom-value-3c"};
            const string path = "/this/is/user/path";

            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId2, null,
                new Dictionary<string, StringValues>
                {
                    {headerKey1, headerValue1},
                    {headerKey2, headerValue2},
                    {headerKey3, headerValue3}
                },
                $"?customQuery1=customValue1&customQuery2=customValue2&{Constants.QueryParameter.OriginalPath}={WebUtility.UrlEncode(path)}"));

            var connection2 = await connectionTask2.OrTimeout();

            Assert.Equal(2, proxy.ClientConnectionManager.ClientConnections.Count);

            var httpContext2 = connection2.GetHttpContext();
            Assert.NotNull(httpContext2);
            Assert.Equal(3, httpContext2.Request.Headers.Count);
            Assert.Equal(headerValue1, httpContext2.Request.Headers[headerKey1]);
            Assert.Equal(headerValue2, httpContext2.Request.Headers[headerKey2]);
            Assert.Equal(headerValue3, httpContext2.Request.Headers[headerKey3]);
            Assert.Equal(3, httpContext2.Request.Query.Count);
            Assert.Equal("customValue1", httpContext2.Request.Query["customQuery1"]);
            Assert.Equal("customValue2", httpContext2.Request.Query["customQuery2"]);
            Assert.Equal(path, httpContext2.Request.Query[Constants.QueryParameter.OriginalPath]);
            Assert.Equal(path, httpContext2.Request.Path);

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
        public async Task ThrowingExceptionAfterServiceErrorMessage()
        {
            var proxy = new ServiceConnectionProxy();

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            string errorMessage = "Maximum message count limit reached: 100000";

            await proxy.WriteMessageAsync(new ServiceErrorMessage(errorMessage));
            await Task.Delay(200);

            var serviceConnection = proxy.ServiceConnection;
            var excption = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                serviceConnection.WriteAsync(new ConnectionDataMessage("1", null)));
            Assert.Equal(errorMessage, excption.Message);
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

        /// <summary>
        /// When keep-alive is timed out, service connection should start reconnecting to service.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ReconnectWhenKeepAliveFailed()
        {
            var proxy = new ServiceConnectionProxy();

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            // Wait for 35s to make the server side timeout
            // Assert the server will reconnect
            var serverTask2 = proxy.WaitForServerConnectionAsync(2);
            Assert.False(Task.WaitAll(new Task[] {serverTask2}, TimeSpan.FromSeconds(1)));

            await Task.Delay(TimeSpan.FromSeconds(35));

            await serverTask2.OrTimeout();
        }

        /// <summary>
        /// When having intermittent connectivity failure, service connection should keep reconnecting to service.
        /// </summary>
        [Fact]
        public async Task ReconnectWhenHavingIntermittentConnectivityFailure()
        {
            var connectionFactory = new TestConnectionFactory(new IntermittentConnectivityFailure().ConnectCallback);
            var proxy = new ServiceConnectionProxy(connectionFactory: connectionFactory);

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var connectionId = Guid.NewGuid().ToString("N");

            var connectionTask = proxy.WaitForConnectionAsync(connectionId);
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId, null));
            await connectionTask.OrTimeout();
        }

        /// <summary>
        /// Service connection should stop reconnecting to service after receiving a handshake response with error message.
        /// </summary>
        [Fact]
        public async Task StopReconnectAfterReceivingHandshakeErrorMessage()
        {
            var proxy = new ServiceConnectionProxy(connectionFactory: new TestConnectionFactoryWithHandshakeError());

            var serverTask = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var connectionId = Guid.NewGuid().ToString("N");
            var connectionTask = proxy.WaitForConnectionAsync(connectionId);

            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId, null));
            
            // Connection exits so the Task should be timeout
            Assert.False(Task.WaitAll(new Task[] {connectionTask}, TimeSpan.FromSeconds(1)));
        }

        /// <summary>
        /// Service connection should keep reconnecting to service when receiving invalid handshake response messages intermittently.
        /// </summary>
        [Fact]
        public async Task ReconnectWhenHandshakeThrowException()
        {
            var connectionFactory = new TestConnectionFactory(new IntermittentInvalidHandshakeResponseMessage().ConnectCallback);
            var proxy = new ServiceConnectionProxy(connectionFactory: connectionFactory);

            // Throw exception for 3 times and will be success in the 4th retry
            var serverTask = proxy.WaitForServerConnectionAsync(4);
            _ = proxy.StartAsync();
            await serverTask.OrTimeout();

            var connectionId = Guid.NewGuid().ToString("N");

            var connectionTask = proxy.WaitForConnectionAsync(connectionId);
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId, null));
            await connectionTask.OrTimeout();
        }

        /// <summary>
        /// Service connection should keep reconnecting to service when it is closed after handshake complete.
        /// </summary>
        [Fact]
        public async Task ReconnectWhenConnectionThrowException()
        {
            var proxy = new ServiceConnectionProxy();

            var serverTask1 = proxy.WaitForServerConnectionAsync(1);
            _ = proxy.StartAsync();
            var serverConnection1 = await serverTask1.OrTimeout();

            // Try to wait the second handshake after reconnect
            var serverTask2 = proxy.WaitForServerConnectionAsync(2);
            Assert.False(Task.WaitAll(new Task[] {serverTask2 }, TimeSpan.FromSeconds(1)));

            // Dispose the connection, then server will throw exception and reconnect
            serverConnection1.Transport.Input.CancelPendingRead();

            await serverTask2.OrTimeout();

            // Verify the server connection works well
            var connectionId = Guid.NewGuid().ToString("N");
            var connectionTask = proxy.WaitForConnectionAsync(connectionId);
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId, null));

            await connectionTask.OrTimeout();
        }

        private static async Task<T> ReadServiceMessageAsync<T>(PipeReader input, int timeout = 5000)
            where T : ServiceMessage
        {
            var data = await input.ReadSingleAsync().OrTimeout(timeout);
            var buffer = new ReadOnlySequence<byte>(data);
            Assert.True(Protocol.TryParseMessage(ref buffer, out var message));
            return Assert.IsType<T>(message);
        }

        private class IntermittentConnectivityFailure
        {
            private const int MaxErrorCount = 3;

            private int _connectCount;

            public Task ConnectCallback(TestConnection connection)
            {
                // Throw exception to test reconnect
                if (_connectCount < MaxErrorCount)
                {
                    _connectCount++;
                    throw new Exception("Connect error.");
                }

                return Task.CompletedTask;
            }
        }

        private class TestConnectionFactoryWithHandshakeError : TestConnectionFactory
        {
            protected override async Task DoHandshakeAsync(TestConnection connection)
            {
                await HandshakeUtils.ReceiveHandshakeRequestAsync(connection.Application.Input);
                await HandshakeUtils.SendHandshakeResponseAsync(connection.Application.Output,
                    new HandshakeResponseMessage("Handshake error."));
            }
        }

        private class IntermittentInvalidHandshakeResponseMessage
        {
            private const int MaxErrorCount = 3;

            private int _connectCount;

            public async Task ConnectCallback(TestConnection connection)
            {
                // Throw exception to test reconnect
                if (_connectCount < MaxErrorCount)
                {
                    _connectCount++;
                    Protocol.WriteMessage(PingMessage.Instance, connection.Application.Output);
                    await connection.Application.Output.FlushAsync();
                }
            }
        }
    }
}
