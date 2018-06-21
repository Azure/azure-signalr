// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
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

            await proxy.StartAsync().OrTimeout();

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

            // Wait for the connection to appear
            var connectionTask2 = proxy.WaitForConnectionAsync(connectionId2);

            // Create another client connection
            const string headerKey1 = "custom-header-1";
            const string headerValue1 = "custom-value-1";
            const string headerKey2 = "custom-header-2";
            var headerValue2 = new[] {"custom-value-2a", "custom-value-2b"};
            const string headerKey3 = "custom-header-3";
            var headerValue3 = new[] {"custom-value-3a", "custom-value-3b", "custom-value-3c"};
            
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId2, null, new Dictionary<string, StringValues> 
            {
                {headerKey1, headerValue1},
                {headerKey2, headerValue2},
                {headerKey3, headerValue3}
            }, "?customQuery1=customValue1&customQuery2=customValue2"));

            var connection2 = await connectionTask2.OrTimeout();

            Assert.Equal(2, proxy.ClientConnectionManager.ClientConnections.Count);

            var httpContext2 = connection2.GetHttpContext();
            Assert.NotNull(httpContext2);
            Assert.Equal(3, httpContext2.Request.Headers.Count);
            Assert.Equal(headerValue1, httpContext2.Request.Headers[headerKey1]);
            Assert.Equal(headerValue2, httpContext2.Request.Headers[headerKey2]);
            Assert.Equal(headerValue3, httpContext2.Request.Headers[headerKey3]);
            Assert.Equal(2, httpContext2.Request.Query.Count);
            Assert.Equal("customValue1", httpContext2.Request.Query["customQuery1"]);
            Assert.Equal("customValue2", httpContext2.Request.Query["customQuery2"]);

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

            await proxy.StartAsync();

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

            await proxy.StartAsync();

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

            await proxy.StartAsync();

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

            await proxy.StartAsync();

            // Check more than once since it happens periodically
            for (int i = 0; i < 2; i++)
            {
                await ReadServiceMessageAsync<PingMessage>(proxy.ConnectionContext.Application.Input, 6000);
            }

            proxy.Stop();
        }

        private async Task<T> ReadServiceMessageAsync<T>(PipeReader input, int timeout = 5000) where T : ServiceMessage
        {
            var data = await input.ReadSingleAsync().OrTimeout(timeout);
            var buffer = new ReadOnlySequence<byte>(data);
            Assert.True(Protocol.TryParseMessage(ref buffer, out var message));
            return Assert.IsType<T>(message);
        }
    }
}
