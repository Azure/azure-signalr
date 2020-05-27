using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceHubDispatcherTests
    {
        [Fact(Skip = "CI failed. Need to fix later.")]
        public async void TestShutdown()
        {
            var clientManager = new TestClientConnectionManager();
            var serviceManager = new TestServiceConnectionManager<Hub>();

            var options = new TestOptions();
            options.Value.GracefulShutdown = new GracefulShutdownOptions()
            {
                Timeout = TimeSpan.FromSeconds(1),
                Mode = GracefulShutdownMode.WaitForClientsClose
            };

            var dispatcher = new ServiceHubDispatcher<Hub>(
                null,
                serviceManager,
                clientManager,
                null,
                options,
                NullLoggerFactory.Instance,
                new TestRouter(),
                null,
                null,
                null
            );

            await dispatcher.ShutdownAsync();

            Assert.True(clientManager.completeTime.Subtract(serviceManager.offlineTime) > TimeSpan.FromMilliseconds(100));
            Assert.True(clientManager.completeTime.Subtract(serviceManager.stopTime) < -TimeSpan.FromMilliseconds(100));
            Assert.True(serviceManager.offlineTime != serviceManager.stopTime);
        }

        private sealed class TestRouter : IEndpointRouter
        {
            public IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> endpoints)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints)
            {
                throw new NotImplementedException();
            }

            public ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class TestClientConnectionManager : IClientConnectionManager
        {
            public IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections => throw new NotImplementedException();

            public DateTime completeTime = new DateTime();

            public void AddClientConnection(ClientConnectionContext clientConnection)
            {
                throw new NotImplementedException();
            }

            public ClientConnectionContext RemoveClientConnection(string connectionId)
            {
                throw new NotImplementedException();
            }

            public async Task WhenAllCompleted()
            {
                await Task.Delay(100);
                completeTime = DateTime.Now;
            }
        }

        private sealed class TestOptions : IOptions<ServiceOptions>
        {
            public ServiceOptions Value { get; } = new ServiceOptions();
        }

        private sealed class TestServiceConnectionManager<THub> : IServiceConnectionManager<THub> where THub : Hub
        {
            public DateTime offlineTime = new DateTime();
            public DateTime stopTime = new DateTime();

            public async Task OfflineAsync(GracefulShutdownMode mode)
            {
                await Task.Delay(100);
                offlineTime = DateTime.Now;
            }

            public void SetServiceConnection(IServiceConnectionContainer serviceConnection)
            {
                throw new NotImplementedException();
            }

            public Task StartAsync()
            {
                throw new NotImplementedException();
            }

            public async Task StopAsync()
            {
                await Task.Delay(100);
                stopTime = DateTime.Now;
            }

            public Task WriteAckableMessageAsync(ServiceMessage seviceMessage, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task WriteAsync(ServiceMessage seviceMessage)
            {
                throw new NotImplementedException();
            }
        }

    }
}
