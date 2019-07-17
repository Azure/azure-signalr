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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    public class MultiEndpointServiceConnectionContainerTests : VerifiableLoggedTest
    {
        private const string ConnectionStringFormatter = "Endpoint={0};AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string Url1 = "http://url1";
        private const string Url2 = "https://url2";
        private readonly string ConnectionString1 = string.Format(ConnectionStringFormatter, Url1);
        private readonly string ConnectionString2 = string.Format(ConnectionStringFormatter, Url2);
        private static readonly JoinGroupWithAckMessage DefaultGroupMessage = new JoinGroupWithAckMessage("a", "a", -1);

        public MultiEndpointServiceConnectionContainerTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestGetRoutedEndpointsReturnDistinctResultForMultiMessages()
        {
            var endpoints = new[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "2"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "11"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "12")
            };

            var sem = new TestServiceEndpointManager(endpoints);

            var router = new TestEndpointRouter();
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            var result = container.GetRoutedEndpoints(new MultiGroupBroadcastDataMessage(new[] { "group1", "group2" }, null)).ToList();

            Assert.Equal(2, result.Count);

            result = container.GetRoutedEndpoints(new MultiUserDataMessage(new[] { "user1", "user2" }, null)).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void TestEndpointManagerWithDuplicateEndpoints()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString1, EndpointType.Secondary, "2"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "11"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "12")
                );
            var endpoints = sem.Endpoints;
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);

            var router = new TestEndpointRouter();
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            Assert.Equal(2, container.Connections.Count);
        }

        [Fact]
        public async Task TestEndpointManagerWithDuplicateEndpointsAndConnectionStarted()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString1, EndpointType.Secondary, "2"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "11"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "12")
                );
            var endpoints = sem.Endpoints;
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);

            var router = new TestEndpointRouter();
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            endpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);
            Assert.Equal(2, container.Connections.Count);
        }

        [Fact]
        public void TestContainerWithNoEndpointDontThrowFromBaseClass()
        {
            var manager = new TestServiceEndpointManager();
            var endpoints = manager.Endpoints;
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
            var throws = new ServiceConnectionNotActiveException();
            var router = new TestEndpointRouter(throws);

            var writeTcs = new TaskCompletionSource<object>();
            TestBaseServiceConnectionContainer innerContainer = null;
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => innerContainer = new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
            }, e), sem, router, null);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            innerContainer.HandleAck(new AckMessage(1, AckStatus.Ok));
            await task.OrTimeout();
        }


        [Fact]
        public async Task TestContainerWithBadRouterThrows()
        {
            var sem = new TestServiceEndpointManager(new ServiceEndpoint(ConnectionString1));
            var throws = new ServiceConnectionNotActiveException();
            var router = new TestEndpointRouter(throws);
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            }, e), sem, router, null);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            await Assert.ThrowsAsync(throws.GetType(),
                () => container.WriteAsync(DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoConnectedEndpointAndBadRouterThrows()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1), 
                new ServiceEndpoint(ConnectionString2));

            var router = new TestEndpointRouter(new ServiceConnectionNotActiveException());
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllConnectedSucceedsWithGoodRouter()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2));

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var containers = new Dictionary<ServiceEndpoint, TestBaseServiceConnectionContainer>();
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => containers[e] = new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
                new TestServiceConnection(writeAsyncTcs: writeTcs),
            }, e), sem, router, null);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            containers.First().Value.HandleAck(new AckMessage(1, AckStatus.Ok));
            await task.OrTimeout();
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllOfflineSucceedsWithWarning()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, logChecker: logs =>
            {
                var warns = logs.Where(s => s.Write.LogLevel == LogLevel.Warning).ToList();
                Assert.Equal(2, warns.Count);
                Assert.Contains(warns, s => s.Write.Message.Contains("Message JoinGroupWithAckMessage is not sent to endpoint (Primary)http://url1 because all connections to this endpoint are offline."));
                return true;
            }))
            {
                var sem = new TestServiceEndpointManager(
                    new ServiceEndpoint(ConnectionString1),
                    new ServiceEndpoint(ConnectionString2));

                var router = new TestEndpointRouter();
                var container = new MultiEndpointServiceConnectionContainer("hub",
                    e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                }, e), sem, router, loggerFactory);

                // All the connections started
                _ = container.StartAsync();
                await container.ConnectionInitializedTask;
                await container.WriteAckableMessageAsync(DefaultGroupMessage);
            }
        }

        [Fact]
        public async Task TestContainerWithTwoOfflineEndpointWriteAckableMessageSucceedsWithWarning()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, logChecker: logs =>
            {
                var warns = logs.Where(s => s.Write.LogLevel == LogLevel.Warning).ToList();
                Assert.Equal(2, warns.Count);
                Assert.Contains(warns, s => s.Write.Message.Contains("Message JoinGroupWithAckMessage is not sent to endpoint (Primary)http://url1 because all connections to this endpoint are offline."));
                return true;
            }))
            {
                var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2));

                var router = new TestEndpointRouter();
                var container = new MultiEndpointServiceConnectionContainer("hub", e =>
                {
                    return new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                    }, e);
                }, sem, router, loggerFactory);

                _ = container.StartAsync();
                await container.ConnectionInitializedTask;

                await container.WriteAckableMessageAsync(DefaultGroupMessage);
            }
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithOneOfflineSucceeds()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
            {
                var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

                var router = new TestEndpointRouter();
                var writeTcs = new TaskCompletionSource<object>();
                var containers = new Dictionary<ServiceEndpoint, TestBaseServiceConnectionContainer>();
                var container = new MultiEndpointServiceConnectionContainer("hub", e =>
                {
                    if (string.IsNullOrEmpty(e.Name))
                    {
                        return containers[e] = new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        }, e);
                    }
                    return containers[e] = new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        }, e);
                }, sem, router, loggerFactory);

                _ = container.StartAsync();
                var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
                await writeTcs.Task.OrTimeout();
                containers.First(p => !string.IsNullOrEmpty(p.Key.Name)).Value.HandleAck(new AckMessage(1, AckStatus.Ok));
                await task.OrTimeout();
            }
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithPrimaryOfflineAndConnectionStartedSucceeds()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "online"));

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var containers = new Dictionary<ServiceEndpoint, TestBaseServiceConnectionContainer>();
            var container = new MultiEndpointServiceConnectionContainer("hub", e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return containers[e] = new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                    }, e);
                }
                return containers[e] = new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                        new TestServiceConnection(writeAsyncTcs: writeTcs),
                    }, e);
            }, sem, router, null);

            _ = container.StartAsync();

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            containers.First(p => !string.IsNullOrEmpty(p.Key.Name)).Value.HandleAck(new AckMessage(1, AckStatus.Ok));
            await task.OrTimeout();

            var endpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Single(endpoints);

            Assert.Equal("online", endpoints.First().Name);
        }

        [Fact]
        public async Task TestMultiEndpointConnectionWithNotExistEndpointRouter()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
            {
                var sem = new TestServiceEndpointManager(
                    new ServiceEndpoint(ConnectionString1),
                    new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "online"));

                var router = new NotExistEndpointRouter();
                var container = new MultiEndpointServiceConnectionContainer("hub", e =>
                {
                    if (string.IsNullOrEmpty(e.Name))
                    {
                        return new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                    }, e);
                    }
                    return new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                        new TestServiceConnection(),
                    }, e);
                }, sem, router, loggerFactory);

                _ = container.StartAsync();

                await container.WriteAsync(DefaultGroupMessage);
            }
        }

        private class NotExistEndpointRouter : EndpointRouterDecorator
        {
            public override IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints)
            {
                return null;
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints)
            {
                return null;
            }
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

        private class TestEndpointRouter : EndpointRouterDecorator
        {
            private readonly Exception _ex;
            private readonly bool _broken;
            public TestEndpointRouter(Exception ex = null) : base()
            {
                _ex = ex;
                _broken = ex != null;
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw _ex;
                }

                return base.GetEndpointsForBroadcast(endpoints);
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw _ex;
                }

                return base.GetEndpointsForConnection(connectionId, endpoints);
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw _ex;
                }

                return base.GetEndpointsForGroup(groupName, endpoints);
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw _ex;
                }

                return base.GetEndpointsForUser(userId, endpoints);
            }

            public override ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw _ex;
                }

                return base.GetNegotiateEndpoint(context, endpoints);
            }
        }
    }
}
