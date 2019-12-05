using System;
using System.Collections.Generic;
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

        [Fact]
        public async Task TestOffline()
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

            await container.OfflineAsync();

            foreach (SimpleTestServiceConnection c in connections)
            {
                Assert.True(c.ConnectionOfflineTask.IsCompleted);
            }
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

            public Task ConnectionOfflineTask => _offline.Task;

            public SimpleTestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Disconnected)
            {
                Status = status;
            }

            public event Action<StatusChange> ConnectionStatusChanged;

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
                if (serviceMessage is PingMessage ping && ping.TryGetValue(Constants.ServicePingMessageKey.ShutdownKey, out var val) && val == Constants.ServicePingMessageValue.ShutdownFin)
                {
                    _offline.SetResult(true);
                }
                return Task.CompletedTask;
            }
        }
    }
}
