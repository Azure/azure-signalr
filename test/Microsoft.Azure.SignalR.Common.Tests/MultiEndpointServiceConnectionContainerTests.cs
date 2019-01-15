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
        private const string ValidConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";

        private static readonly JoinGroupMessage DefaultGroupMessage = new JoinGroupMessage("a", "a");

        [Fact]
        public void TestContainerWithNoEndpointThrows()
        {
            var hub = "hub1";
            var count = 1;
            var sem = new TestServiceEndpointManager();
            var router = new TestEndpointRouter(false);
            Assert.Throws<AzureSignalRNoEndpointAvailableException>(() => new MultiEndpointServiceConnectionContainer(CreateServiceConnection, hub, count, sem, router, null));
        }

        [Fact]
        public async Task TestContainerWithOneEndpointWithAllConnectedSucceeeds()
        {
            var inner = new StrongServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            });

            var sem = new TestServiceEndpointManager(new ServiceEndpoint(ValidConnectionString));
            var router = new TestEndpointRouter(true);
            var container = new MultiEndpointServiceConnectionContainer(e => inner, sem, router, null);
            await container.WriteAsync(DefaultGroupMessage);

            await container.WriteAsync("1", DefaultGroupMessage);
        }

        [Fact]
        public async Task TestContainerWithOneEndpointWithAllDisconnectedThrows()
        {
            var inner = new StrongServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            });

            var sem = new TestServiceEndpointManager(new ServiceEndpoint(ValidConnectionString));
            var router = new TestEndpointRouter(true);
            var container = new MultiEndpointServiceConnectionContainer(e => inner, sem, router, null);

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
            var inner = new StrongServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            });

            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ValidConnectionString), 
                new ServiceEndpoint(Constants.ConnectionStringKeyPrefix + "2", ValidConnectionString));

            var router = new TestEndpointRouter(true);
            var container = new MultiEndpointServiceConnectionContainer(e => inner, sem, router, null);
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
            var inner = new StrongServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            });

            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ValidConnectionString),
                new ServiceEndpoint(Constants.ConnectionStringKeyPrefix + "2", ValidConnectionString));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(e => inner, sem, router, null);
            await container.WriteAsync(DefaultGroupMessage);

            await container.WriteAsync("1", DefaultGroupMessage);
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllDisconnectedThrows()
        {
            var inner = new StrongServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            });

            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ValidConnectionString),
                new ServiceEndpoint(Constants.ConnectionStringKeyPrefix + "2", ValidConnectionString));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(e => inner, sem, router, null);

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync("1", DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithOneAllDisconnectedThrows()
        {
            var inner1 = new StrongServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            });

            var inner2 = new StrongServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            });

            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ValidConnectionString),
                new ServiceEndpoint(Constants.ConnectionStringKeyPrefix + "2", ValidConnectionString));

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer(e =>
            {
                if (e.Key == Constants.ConnectionStringDefaultKey)
                {
                    return inner1;
                }

                return inner2;
            }, sem, router, null);

            // TODO: when online & offline is enabled, should not throw then
            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync("1", DefaultGroupMessage)
                );
        }

        private IServiceConnection CreateServiceConnection(ServerConnectionType type, IConnectionFactory factory)
        {
            return new TestServiceConnection();
        }

        private class TestServiceEndpointManager : IServiceEndpointManager
        {
            private readonly ServiceEndpoint[] _endpoints;
            public TestServiceEndpointManager(params ServiceEndpoint[] endpoints)
            {
                _endpoints = endpoints;
            }

            public IReadOnlyList<ServiceEndpoint> GetAvailableEndpoints()
            {
                return _endpoints;
            }

            public IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
            {
                return null;
            }

            public IReadOnlyList<ServiceEndpoint> GetPrimaryEndpoints()
            {
                return _endpoints.Where(s => s.EndpointType == EndpointType.Primary).ToArray();
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
            public IReadOnlyList<ServiceEndpoint> GetEndpointsForBroadcast(IReadOnlyList<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForBroadcast(availableEnpoints);
            }

            public IReadOnlyList<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IReadOnlyList<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForConnection(connectionId, availableEnpoints);
            }

            public IReadOnlyList<ServiceEndpoint> GetEndpointsForGroup(string groupName, IReadOnlyList<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForGroup(groupName, availableEnpoints);
            }

            public IReadOnlyList<ServiceEndpoint> GetEndpointsForGroups(IReadOnlyList<string> groupList, IReadOnlyList<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForGroups(groupList, availableEnpoints);
            }

            public IReadOnlyList<ServiceEndpoint> GetEndpointsForUser(string userId, IReadOnlyList<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForUser(userId, availableEnpoints);
            }

            public IReadOnlyList<ServiceEndpoint> GetEndpointsForUsers(IReadOnlyList<string> userList, IReadOnlyList<ServiceEndpoint> availableEnpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return _inner.GetEndpointsForUsers(userList, availableEnpoints);
            }

            public ServiceEndpoint GetNegotiateEndpoint(IReadOnlyList<ServiceEndpoint> primaryEndpoints)
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

            private readonly bool _throws;
            public TestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected, bool throws = false)
            {
                Status = status;
                _throws = throws;
            }

            public Task StartAsync()
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
