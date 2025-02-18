// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceConnectionContainerTests
{
    [Fact]
    public async Task TestCloseAsync()
    {
        var conn = new TestServiceConnectionForCloseAsync();
        var hub = new TestHubServiceEndpoint();
        using var container = new TestBaseServiceConnectionContainer(new List<IServiceConnection> { conn }, hub);

        _ = conn.StartAsync();
        _ = MockServiceAsync(conn);

        // close connection after 1 seconds.
        await Task.Delay(TimeSpan.FromSeconds(1));
        // await AssertTask(container.CloseClientConnectionForTest(conn), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TestCloseAsyncWithoutStartAsync()
    {
        var conn = new TestServiceConnectionForCloseAsync();
        var hub = new TestHubServiceEndpoint();
        using var container = new TestBaseServiceConnectionContainer(new List<IServiceConnection> { conn }, hub);

        // await AssertTask(container.CloseClientConnectionForTest(conn), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TestCloseAsyncWithExceptionAndNoFinAck()
    {
        var conn = new TestServiceConnectionForCloseAsync();
        var hub = new TestHubServiceEndpoint();
        using var container = new TestBaseServiceConnectionContainer(new List<IServiceConnection> { conn }, hub);

        _ = conn.StartAsync();
        _ = MockServiceAsyncWithException(conn);

        // close connection after 2 seconds to make sure we have received an exception.
        await Task.Delay(TimeSpan.FromSeconds(2));
        // TODO double check if we received an exception.
        // await AssertTask(container.CloseClientConnectionForTest(conn), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TestServiceConnectionOffline()
    {
        var factory = new TestServiceConnectionFactory();
        var hubServiceEndpoint = new HubServiceEndpoint("foo", null, new TestServiceEndpoint());

        var container = new StrongServiceConnectionContainer(factory, 3, 3, hubServiceEndpoint, NullLogger.Instance);

        Assert.True(factory.CreatedConnections.TryGetValue(hubServiceEndpoint, out var conns));
        var connections = conns.Select(x => (TestServiceConnection)x).ToArray();

        foreach (var connection in connections)
        {
            connection.SetStatus(ServiceConnectionStatus.Connected);
        }

        // write 100 messages.
        for (var i = 0; i < 100; i++)
        {
            var message = new ConnectionDataMessage(i.ToString(CultureInfo.InvariantCulture), new byte[12]);
            await container.WriteAsync(message);
        }

        var messageCount = new Dictionary<string, int>();
        foreach (var connection in connections)
        {
            Assert.NotEmpty(connection.ReceivedMessages);
            messageCount.TryAdd(connection.ConnectionId, connection.ReceivedMessages.Count);
        }

        connections[0].SetStatus(ServiceConnectionStatus.Disconnected);

        // write 100 more messages.
        for (var i = 0; i < 100; i++)
        {
            var message = new ConnectionDataMessage(i.ToString(CultureInfo.InvariantCulture), new byte[12]);
            await container.WriteAsync(message);
        }

        var index = 0;
        foreach (var connection in connections)
        {
            if (index == 0)
            {
                Assert.Equal(messageCount[connection.ConnectionId], connection.ReceivedMessages.Count);
            }
            else
            {
                Assert.NotEqual(messageCount[connection.ConnectionId], connection.ReceivedMessages.Count);
            }
            index++;
        }
    }

    [Fact]
    public async Task TestServiceConnectionStickyWrites()
    {
        var factory = new TestServiceConnectionFactory();
        var hubServiceEndpoint = new HubServiceEndpoint("foo", null, new TestServiceEndpoint());

        var container = new StrongServiceConnectionContainer(factory, 30, 30, hubServiceEndpoint, NullLogger.Instance);

        Assert.True(factory.CreatedConnections.TryGetValue(hubServiceEndpoint, out var conns));
        var connections = conns.Select(x => (TestServiceConnection)x);

        foreach (var connection in connections)
        {
            connection.SetStatus(ServiceConnectionStatus.Connected);
        }

        // write 100000 messages.
        for (var i = 0; i < 100000; i++)
        {
            var message = new ConnectionDataMessage(i.ToString(CultureInfo.InvariantCulture), new byte[12]);
            await container.WriteAsync(message);
        }

        var messageCount = new Dictionary<string, int>();
        foreach (var connection in connections)
        {
            Assert.NotEmpty(connection.ReceivedMessages);
            messageCount.TryAdd(connection.ConnectionId, connection.ReceivedMessages.Count);
        }

        // write 100000 messages with the same connectionIds should double the message count for each service connection
        for (var i = 0; i < 100000; i++)
        {
            var message = new ConnectionDataMessage(i.ToString(CultureInfo.InvariantCulture), new byte[12]);
            await container.WriteAsync(message);
        }

        foreach (var connection in connections)
        {
            Assert.Equal(messageCount[connection.ConnectionId] * 2, connection.ReceivedMessages.Count);
        }

        // Offline half of the connections
        foreach (var connection in connections.Skip(15))
        {
            connection.SetStatus(ServiceConnectionStatus.Disconnected);
        }

        var sub = connections.SkipLast(15).Sum(s => s.ReceivedMessages.Count) - connections.Skip(15).Sum(s => s.ReceivedMessages.Count);

        // write 100000 messages with the same connectionIds does not throw
        for (var i = 0; i < 100000; i++)
        {
            var message = new ConnectionDataMessage(i.ToString(CultureInfo.InvariantCulture), new byte[12]);
            await container.WriteAsync(message);
        }

        // messages are all going through the connected connections
        var disconnected = connections.Skip(15).Sum(s => s.ReceivedMessages.Count);
        var connected = connections.SkipLast(15).Sum(s => s.ReceivedMessages.Count);
        Assert.Equal(100000 + sub, connected - disconnected);
    }

    [Fact]
    public async Task TestServiceConnectionStickyWritesWithScope()
    {
        // with scope enabled, the messages always go through the first picked connection
        using var _ = new ClientConnectionScope();
        var factory = new TestServiceConnectionFactory();
        var hubServiceEndpoint = new HubServiceEndpoint("foo", null, new TestServiceEndpoint());

        var container = new StrongServiceConnectionContainer(factory, 30, 30, hubServiceEndpoint, NullLogger.Instance);

        Assert.True(factory.CreatedConnections.TryGetValue(hubServiceEndpoint, out var conns));
        var connections = conns.Select(x => (TestServiceConnection)x);

        foreach (var connection in connections)
        {
            connection.SetStatus(ServiceConnectionStatus.Connected);
        }

        // write 100000 messages.
        for (var i = 0; i < 100000; i++)
        {
            var message = new ConnectionDataMessage(i.ToString(CultureInfo.InvariantCulture), new byte[12]);
            await container.WriteAsync(message);
        }

        var selected = connections.Where(s => !s.ReceivedMessages.IsEmpty).ToArray();
        Assert.Single(selected);

        Assert.Equal(100000, selected[0].ReceivedMessages.Count);
    }

    private static async Task MockServiceAsync(TestServiceConnectionForCloseAsync conn)
    {
        await conn.ConnectionCreated;

        // open 2 new connections (to create 2 new outgoing tasks
        new ServiceProtocol().WriteMessage(new OpenConnectionMessage(Guid.NewGuid().ToString(), Array.Empty<Claim>()), conn.Application.Output);
        new ServiceProtocol().WriteMessage(new OpenConnectionMessage(Guid.NewGuid().ToString(), Array.Empty<Claim>()), conn.Application.Output);
        await conn.Application.Output.FlushAsync();

        while (true)
        {
            var result = await conn.Application.Input.ReadAsync();
            var buffer = result.Buffer;

            try
            {
                // write back a FinAck after receiving a Fin
                if (new ServiceProtocol().TryParseMessage(ref buffer, out var message))
                {
                    if (RuntimeServicePingMessage.IsFin(message))
                    {
                        var pong = RuntimeServicePingMessage.GetFinAckPingMessage();
                        new ServiceProtocol().WriteMessage(pong, conn.Application.Output);
                        await conn.Application.Output.FlushAsync();
                        break;
                    }
                }
            }
            finally
            {
                conn.Application.Input.AdvanceTo(buffer.Start, buffer.End);
            }
        }
    }

    private static PingMessage BuildPingMessage(string key, string val)
    {
        return new PingMessage
        {
            Messages = new string[2] { key, val }
        };
    }

    private static async Task MockServiceAsyncWithException(TestServiceConnectionForCloseAsync conn)
    {
        await conn.ConnectionCreated;

        // open 2 new connections (to create 2 new outgoing tasks
        new ServiceProtocol().WriteMessage(new OpenConnectionMessage(Guid.NewGuid().ToString(), Array.Empty<Claim>()), conn.Application.Output);
        new ServiceProtocol().WriteMessage(new OpenConnectionMessage(Guid.NewGuid().ToString(), Array.Empty<Claim>()), conn.Application.Output);
        await conn.Application.Output.FlushAsync();

        await Task.Delay(TimeSpan.FromSeconds(1));
        new ServiceProtocol().WriteMessage(BuildPingMessage("_exception", "1"), conn.Application.Output);
        await conn.Application.Output.FlushAsync();
    }
}
