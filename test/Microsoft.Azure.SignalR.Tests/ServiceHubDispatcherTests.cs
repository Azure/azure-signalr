using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceHubDispatcherTests
{
    [Fact]
    public async void TestShutdown()
    {
        var index = new StrongBox<int>();
        var clientManager = new TestClientConnectionManager(index);
        var serviceManager = new TestServiceConnectionManager<Hub>(index);

        var options = new TestOptions();
        options.Value.GracefulShutdown = new GracefulShutdownOptions()
        {
            Timeout = TimeSpan.FromSeconds(1),
            Mode = GracefulShutdownMode.WaitForClientsClose
        };

        var dispatcher = new ServiceHubDispatcher<Hub>(
            null,
            TestHubContext<Hub>.GetInstance(),
            serviceManager,
            clientManager,
            null,
            options,
            NullLoggerFactory.Instance,
            new TestRouter(),
            null,
            null,
            null,
            null,
            null,
            new DefaultHubProtocolResolver(new[] { new JsonHubProtocol() }, NullLogger<DefaultHubProtocolResolver>.Instance)
        );

        await dispatcher.ShutdownAsync();

        Assert.Equal(3, serviceManager.StopIndex);
        Assert.Equal(2, clientManager.CompleteIndex);
        Assert.Equal(1, serviceManager.OfflineIndex);
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
        public int CompleteIndex = -1;

        private readonly StrongBox<int> _index;

        public IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections => throw new NotImplementedException();

        public TestClientConnectionManager(StrongBox<int> index)
        {
            _index = index;
        }

        public async Task WhenAllCompleted()
        {
            await Task.Yield();
            CompleteIndex = Interlocked.Increment(ref _index.Value);
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
        public int OfflineIndex = -1;

        public int StopIndex = -1;

        private readonly StrongBox<int> _index;

        public TestServiceConnectionManager(StrongBox<int> index)
        {
            _index = index;
        }

        public async Task OfflineAsync(GracefulShutdownMode mode)
        {
            await Task.Yield();
            OfflineIndex = Interlocked.Increment(ref _index.Value);
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
            StopIndex = Interlocked.Increment(ref _index.Value);
        }

        public Task<bool> WriteAckableMessageAsync(ServiceMessage seviceMessage, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task WriteAsync(ServiceMessage seviceMessage)
        {
            throw new NotImplementedException();
        }
    }
}
