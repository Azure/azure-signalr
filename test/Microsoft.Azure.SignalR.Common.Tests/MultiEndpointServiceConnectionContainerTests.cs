// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class MultiEndpointServiceConnectionContainerTests
    {
        private const string ConnectionStringFormatter = "Endpoint={0};AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string Url1 = "http://url1";
        private const string Url2 = "https://url2";
        private readonly string ConnectionString1 = string.Format(ConnectionStringFormatter, Url1);
        private readonly string ConnectionString2 = string.Format(ConnectionStringFormatter, Url2);
        private static readonly JoinGroupMessage DefaultGroupMessage = new JoinGroupMessage("a", "a");

        [Fact]
        public void TestEndpointManagerWithDuplicateEndpoints()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString1, EndpointType.Secondary, "2"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "11"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "12")
                );
            var endpoints = sem.GetAvailableEndpoints().ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);

            var primaryEndpoints = sem.GetPrimaryEndpoints().ToArray();
            Assert.Single(primaryEndpoints);
            Assert.Equal("1", primaryEndpoints[0].Name);

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            Assert.Equal(2, container.Connections.Count);
        }

        [Fact]
        public void TestEndpointManagerWithDuplicateEndpointsAndConnectionStarted()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString1, EndpointType.Secondary, "2"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "11"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "12")
                );
            var endpoints = sem.GetAvailableEndpoints().ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);

            var primaryEndpoints = sem.GetPrimaryEndpoints().ToArray();
            Assert.Single(primaryEndpoints);
            Assert.Equal("1", primaryEndpoints[0].Name);

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            // All the connections started
            _ = container.StartAsync();

            endpoints = sem.GetAvailableEndpoints().ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);

            primaryEndpoints = sem.GetPrimaryEndpoints().ToArray();
            Assert.Single(primaryEndpoints);
            Assert.Equal("1", primaryEndpoints[0].Name);
            Assert.Equal(2, container.Connections.Count);
        }

        [Fact]
        public void TestContainerWithNoEndpointDontThrowFromBaseClass()
        {
            var manager = new TestServiceEndpointManager();
            var endpoints = manager.GetAvailableEndpoints();
            Assert.Empty(endpoints);
        }

        [Fact]
        public void TestContainerWithNoPrimaryEndpointDefinedThrows()
        {
            Assert.Throws<AzureSignalRNoPrimaryEndpointException>(() => new TestServiceEndpointManager(new ServiceEndpoint[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Secondary)
            }));
        }

        [Fact]
        public async Task TestContainerWithOneEndpointWithAllConnectedSucceeeds()
        {
            var sem = new TestServiceEndpointManager(new ServiceEndpoint(ConnectionString1));
            var router = new TestEndpointRouter(true);
            var container = new MultiEndpointServiceConnectionContainer(
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);
            await container.WriteAsync(DefaultGroupMessage);

            await container.WriteAsync("1", DefaultGroupMessage);
        }


        [Fact]
        public async Task TestContainerWithOneEndpointWithAllDisconnectedAndConnectionStartedThrows()
        {
            var sem = new TestServiceEndpointManager(new ServiceEndpoint(ConnectionString1));
            var router = new TestEndpointRouter(true);
            var container = new MultiEndpointServiceConnectionContainer(
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            }, e), sem, router, null);

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync("1", DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllConnectedFailsWithBadRouter()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1), 
                new ServiceEndpoint(ConnectionString2));

            var router = new TestEndpointRouter(true);
            var container = new MultiEndpointServiceConnectionContainer(
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => container.WriteAsync("1", DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllConnectedSucceedsWithGoodRouter()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);
            await container.WriteAsync(DefaultGroupMessage);

            await container.WriteAsync("1", DefaultGroupMessage);
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllOfflineThrows()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            }, e), sem, router, null);

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync("1", DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllOfflineAndConnectionStartedThrows()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            }, e), sem, router, null);

            _ = container.StartAsync();

            // Instead of NotActiveException, throws NotConnectedException
            await Assert.ThrowsAsync<AzureSignalRNotConnectedException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );

            await Assert.ThrowsAsync<AzureSignalRNotConnectedException>(
                () => container.WriteAsync("1", DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithOneOfflineThrows()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                    }, e);
                }
                return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                    }, e);
            }, sem, router, null);

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync("1", DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithOneOfflineAndConnectionStartedSucceeds()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                    }, e);
                }
                return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                    }, e);
            }, sem, router, null);

            _ = container.StartAsync();

            await container.WriteAsync(DefaultGroupMessage);

            await container.WriteAsync("1", DefaultGroupMessage);
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithPrimaryOfflineAndConnectionStartedSucceeds()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "online"));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                    }, e);
                }
                return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                    }, e);
            }, sem, router, null);

            _ = container.StartAsync();

            await container.WriteAsync(DefaultGroupMessage);

            await container.WriteAsync("1", DefaultGroupMessage);

            var endpoints = sem.GetAvailableEndpoints();
            Assert.Single(endpoints);

            endpoints = sem.GetPrimaryEndpoints();
            Assert.Single(endpoints);

            Assert.Equal("online", endpoints.First().Name);
        }

        private IServiceConnection CreateServiceConnection(ServerConnectionType type, IConnectionFactory factory)
        {
            return new TestServiceConnection();
        }

        private class TestServiceEndpointManager : ServiceEndpointManagerBase
        {
            private readonly ServiceEndpoint[] _endpoints;

            public TestServiceEndpointManager(params ServiceEndpoint[] endpoints) : base(endpoints, null)
            {
                _endpoints = endpoints;
            }

            public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
            {
                return null;
            }
        }

        private class TestEndpointRouter : IEndpointRouter
        {
            private readonly IEndpointRouter _inner = new DefaultRouter();

            private readonly bool _broken;
            public TestEndpointRouter(bool broken)
            {
                _broken = broken;
            }
            public IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForBroadcast(availableEnpoints);
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForConnection(connectionId, availableEnpoints);
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForGroup(groupName, availableEnpoints);
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForGroups(IReadOnlyList<string> groupList, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForGroups(groupList, availableEnpoints);
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForUser(userId, availableEnpoints);
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForUsers(IReadOnlyList<string> userList, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForUsers(userList, availableEnpoints);
            }

            public ServiceEndpoint GetNegotiateEndpoint(IEnumerable<ServiceEndpoint> primaryEndpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetNegotiateEndpoint(primaryEndpoints);
            }
        }

        private sealed class TestServiceConnection : IServiceConnection
        {
            public ServiceConnectionStatus Status { get; }

            public Task ConnectionInitializedTask => Task.CompletedTask;

            private readonly bool _throws;
            public TestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected, bool throws = false)
            {
                Status = status;
                _throws = throws;
            }

            public Task StartAsync(string target = null)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                return Task.CompletedTask;
            }

            public Task WriteAsync(ServiceMessage serviceMessage)
            {
                if (_throws)
                {
                    throw new ServiceConnectionNotActiveException();
                }

                return Task.CompletedTask;
            }
        }
    }
}
