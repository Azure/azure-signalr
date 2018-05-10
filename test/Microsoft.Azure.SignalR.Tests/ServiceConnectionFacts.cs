// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceConnectionFacts
    {
        [Fact]
        public async Task ServiceConnectionStartsConnection()
        {
            var connectionId1 = Guid.NewGuid().ToString("N");
            var connectionId2 = Guid.NewGuid().ToString("N");

            var proxy = new ServiceConnectionProxy();

            await proxy.StartAsync().OrTimeout();

            Assert.Empty(proxy.ClientConnectionManager.ClientConnections);

            // Create a new client connection
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId1, null));

            // Wait for the connection to appear
            var connection1 = await proxy.WaitForConnectionAsync(connectionId1).OrTimeout();

            Assert.Single(proxy.ClientConnectionManager.ClientConnections);

            // Create another client connection
            await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId2, null));

            var connection2 = await proxy.WaitForConnectionAsync(connectionId2).OrTimeout();

            Assert.Equal(2, proxy.ClientConnectionManager.ClientConnections.Count);

            // Send a message to client 1
            await proxy.WriteMessageAsync(new ConnectionDataMessage(connectionId1, Encoding.ASCII.GetBytes("Hello")));

            var item = await connection1.Transport.Input.ReadSingleAsync().OrTimeout();

            Assert.Equal("Hello", Encoding.ASCII.GetString(item));

            // Close client 1
            await proxy.WriteMessageAsync(new CloseConnectionMessage(connectionId1, null));

            await proxy.WaitForConnectionCloseAsync(connectionId1).OrTimeout();

            Assert.Single(proxy.ClientConnectionManager.ClientConnections);

            // Close client 2
            await proxy.WriteMessageAsync(new CloseConnectionMessage(connectionId2, null));

            await proxy.WaitForConnectionCloseAsync(connectionId2).OrTimeout();

            Assert.Empty(proxy.ClientConnectionManager.ClientConnections);

            proxy.Stop();
        }
    }
}
