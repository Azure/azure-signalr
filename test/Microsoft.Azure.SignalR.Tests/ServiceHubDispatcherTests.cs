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
        [Fact]
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

            DateTime now = DateTime.Now;

            Assert.True(now > serviceManager.StopTime);
            Assert.True(serviceManager.StopTime > clientManager.CompleteTime);
            Assert.True(clientManager.CompleteTime> serviceManager.OfflineTime);
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

            public DateTime CompleteTime = new DateTime();

            public async Task WhenAllCompleted()
            {
                await Task.Yield();
                CompleteTime = DateTime.Now;
            }

            public bool TryAddClientConnection(ClientConnectionContext connection)
            {
                throw new NotImplementedException();
            }

            public bool TryRemoveClientConnection(string connectionId, out ClientConnectionContext connection)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class TestOptions : IOptions<ServiceOptions>
        {
            public ServiceOptions Value { get; } = new ServiceOptions();
        }

        private sealed class TestServiceConnectionManager<THub> : IServiceConnectionManager<THub> where THub : Hub
        {
            public DateTime OfflineTime = new DateTime();
            public DateTime StopTime = new DateTime();

            public async Task OfflineAsync(GracefulShutdownMode mode)
            {
                await Task.Yield();
                OfflineTime = DateTime.Now;
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
                await Task.Yield();
                StopTime = DateTime.Now;
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
