using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceConnectionContainerBaseTests
    {
        [Fact]
        public async Task testIfConnectionWillRestartAfterShutdown()
        {
            List<IServiceConnection> connections = new List<IServiceConnection>();
            connections.Add(new SimpleTestServiceConnection());
            connections.Add(new SimpleTestServiceConnection()); // A connection which is not in Connected status could be replaced.

            IServiceConnection connection = connections[1];

            TestServiceConnectionContainer container = new TestServiceConnectionContainer(connections, factory : new SimpleTestServiceConnectionFactory());
            container.ShutdownForTest();

            await container.OnConnectionCompleteForTestShutdown(connection);

            // connection should be replaced, but it's StartAsync method should not be called.
            Assert.NotEqual(container.Connections[1], connection);
            Assert.NotEqual(ServiceConnectionStatus.Connected, container.Connections[1].Status);
        }
    }
}
