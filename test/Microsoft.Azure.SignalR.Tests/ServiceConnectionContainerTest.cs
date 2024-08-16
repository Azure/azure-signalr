// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceConnectionContainerTest
{
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
            var message = new ConnectionDataMessage(i.ToString(), new byte[12]);
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
            var message = new ConnectionDataMessage(i.ToString(), new byte[12]);
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

        var container = new StrongServiceConnectionContainer(factory, 3, 3, hubServiceEndpoint, NullLogger.Instance);

        Assert.True(factory.CreatedConnections.TryGetValue(hubServiceEndpoint, out var conns));
        var connections = conns.Select(x => (TestServiceConnection)x);

        foreach (var connection in connections)
        {
            connection.SetStatus(ServiceConnectionStatus.Connected);
        }

        // write 100 messages.
        for (var i = 0; i < 100; i++)
        {
            var message = new ConnectionDataMessage(i.ToString(), new byte[12]);
            await container.WriteAsync(message);
        }

        var messageCount = new Dictionary<string, int>();
        foreach (var connection in connections)
        {
            Assert.NotEmpty(connection.ReceivedMessages);
            messageCount.TryAdd(connection.ConnectionId, connection.ReceivedMessages.Count);
        }

        // write 100 messages with the same connectionIds should double the message count for each service connection
        for (var i = 0; i < 100; i++)
        {
            var message = new ConnectionDataMessage(i.ToString(), new byte[12]);
            await container.WriteAsync(message);
        }

        foreach (var connection in connections)
        {
            Assert.Equal(messageCount[connection.ConnectionId] * 2, connection.ReceivedMessages.Count);
        }
    }
}
