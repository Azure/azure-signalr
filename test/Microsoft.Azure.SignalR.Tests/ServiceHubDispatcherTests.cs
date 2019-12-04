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
            var dispatcher = new ServiceHubDispatcher<Hub>(
                null,
                serviceManager,
                clientManager,
                null,
                new TestOptions(),
                NullLoggerFactory.Instance,
                new TestRouter(),
                null,
                null
            );

            await dispatcher.ShutdownAsync(TimeSpan.FromSeconds(1));

            long offline = _(ref serviceManager.offlineTicks);
            long stop = _(ref serviceManager.stopTicks);
            long complete = _(ref clientManager.completeTicks);

            Assert.True(complete - offline > TimeSpan.FromMilliseconds(100).Ticks);
            Assert.True(complete - stop < -TimeSpan.FromMilliseconds(100).Ticks);
            Assert.True(offline != stop);
        }

        private long _(ref long a)
        {
            return Interlocked.Read(ref a);
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

            public long completeTicks = new DateTime().Ticks;

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
                completeTicks = DateTime.Now.Ticks;
            }
        }

        private sealed class TestOptions : IOptions<ServiceOptions>
        {
            public ServiceOptions Value => new ServiceOptions();
        }

        private sealed class TestServiceConnectionManager<THub> : IServiceConnectionManager<THub> where THub : Hub
        {
            public long offlineTicks = new DateTime().Ticks;
            public long stopTicks = new DateTime().Ticks;

            public async Task OfflineAsync()
            {
                await Task.Delay(100);
                offlineTicks = DateTime.Now.Ticks;
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
                stopTicks = DateTime.Now.Ticks;
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
