using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceConnectionContainerBaseTests
    {
        [Fact]
        public async Task TestIfConnectionWillRestartAfterShutdown()
        {
            List<IServiceConnection> connections = new List<IServiceConnection>
            {
                new SimpleTestServiceConnection(),
                new SimpleTestServiceConnection() // A connection which is not in Connected status could be replaced.
            };

            IServiceConnection connection = connections[1];

            using TestServiceConnectionContainer container = new TestServiceConnectionContainer(connections, factory: new SimpleTestServiceConnectionFactory());
            container.ShutdownForTest();

            await container.OnConnectionCompleteForTestShutdown(connection);

            // connection should be replaced, but it's StartAsync method should not be called.
            Assert.NotEqual(container.Connections[1], connection);
            Assert.NotEqual(ServiceConnectionStatus.Connected, container.Connections[1].Status);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestOffline(bool migratable)
        {
            List<IServiceConnection> connections = new List<IServiceConnection>
            {
                new SimpleTestServiceConnection(),
                new SimpleTestServiceConnection()
            };
            using TestServiceConnectionContainer container = new TestServiceConnectionContainer(connections, factory: new SimpleTestServiceConnectionFactory());

            foreach (SimpleTestServiceConnection c in connections)
            {
                Assert.False(c.ConnectionOfflineTask.IsCompleted);
            }

            await container.OfflineAsync(migratable);

            foreach (SimpleTestServiceConnection c in connections)
            {
                Assert.True(c.ConnectionOfflineTask.IsCompleted);
            }
        }

        [Fact]
        public async Task TestServerIdsPing()
        {
            List<IServiceConnection> connections = new List<IServiceConnection>
            {
                new SimpleTestServiceConnection(),
                new SimpleTestServiceConnection(),
                new SimpleTestServiceConnection()
            };
            using TestServiceConnectionContainer container = new TestServiceConnectionContainer(connections, factory: new SimpleTestServiceConnectionFactory());

            await container.StartAsync();
            await container.StartGetServersPing();

            // default interval is 5s, add 2s for delay, validate any one connection write servers ping.
            await Task.WhenAny(connections.Select(c => {
                var connection = c as SimpleTestServiceConnection;
                return connection.ServerIdsPingTask.OrTimeout(7000);
            }));

            await container.StopGetServersPing();
        }

        private sealed class SimpleTestServiceConnectionFactory : IServiceConnectionFactory
        {
            public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServiceConnectionType type) => new SimpleTestServiceConnection();
        }

        private sealed class SimpleTestServiceConnection : IServiceConnection
        {
            public Task ConnectionInitializedTask => Task.Delay(TimeSpan.FromSeconds(1));

            public ServiceConnectionStatus Status { get; set; } = ServiceConnectionStatus.Disconnected;

            private readonly TaskCompletionSource<bool> _offline = new TaskCompletionSource<bool>(TaskContinuationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _serverIdsPing = new TaskCompletionSource<bool>(TaskContinuationOptions.RunContinuationsAsynchronously);

            public Task ConnectionOfflineTask => _offline.Task;

            public Task ServerIdsPingTask => _serverIdsPing.Task;

            public SimpleTestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Disconnected)
            {
                Status = status;
            }

            public event Action<StatusChange> ConnectionStatusChanged
            {
                add { }
                remove { }
            }

            public Task StartAsync(string target = null)
            {
                Status = ServiceConnectionStatus.Connected;
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                return Task.CompletedTask;
            }

            public Task WriteAsync(ServiceMessage serviceMessage)
            {
                if (RuntimeServicePingMessage.IsFin(serviceMessage))
                {
                    _offline.SetResult(true);
                }
                if (RuntimeServicePingMessage.IsGetServers(serviceMessage))
                {
                    _serverIdsPing.SetResult(true);
                }
                return Task.CompletedTask;
            }
        }
    }
}
