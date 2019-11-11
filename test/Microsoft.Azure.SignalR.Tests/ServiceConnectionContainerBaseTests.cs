using System;
using System.Collections.Generic;
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

        private sealed class SimpleTestServiceConnectionFactory : IServiceConnectionFactory
        {
            public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, ServerConnectionType type)
            {
                return new SimpleTestServiceConnection();
            }
        }

        private sealed class SimpleTestServiceConnection : IServiceConnection
        {
            public ServiceConnectionStatus Status { get; set; }

            public Task ConnectionInitializedTask => Task.Delay(TimeSpan.FromSeconds(1));

            public Task ConnectionOfflineTask => Task.CompletedTask;

            public event Action<StatusChange> ConnectionStatusChanged;

            public SimpleTestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Disconnected)
            {
                Status = status;
            }

            public Task StartAsync(string target = null)
            {
                Status = ServiceConnectionStatus.Connected;
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                throw new NotImplementedException();
            }

            public Task WriteAsync(ServiceMessage serviceMessage)
            {
                throw new NotImplementedException();
            }
        }
    }
}
