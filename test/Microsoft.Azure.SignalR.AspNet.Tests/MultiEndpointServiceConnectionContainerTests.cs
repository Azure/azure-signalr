// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class MultiEndpointServiceConnectionContainerTests : VerifiableLoggedTest
    {
        private const string ConnectionStringFormatter = "Endpoint={0};AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string Url1 = "http://url1";
        private const string Url2 = "https://url2";
        private readonly string ConnectionString1 = string.Format(ConnectionStringFormatter, Url1);
        private readonly string ConnectionString2 = string.Format(ConnectionStringFormatter, Url2);
        private static readonly JoinGroupWithAckMessage DefaultGroupMessage = new JoinGroupWithAckMessage("a", "a");

        public MultiEndpointServiceConnectionContainerTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task TestGetRoutedEndpointsReturnDistinctResultForMultiMessages()
        {
            var endpoints = new[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "2"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "11"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "12")
            };

            var sem = new TestServiceEndpointManager(endpoints);

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            // Start the container for it to disconnect
            _ = container.StartAsync();
            await container.ConnectionInitializedTask.OrTimeout();

            var result = container.GetRoutedEndpoints(new MultiGroupBroadcastDataMessage(new[] { "group1", "group2" }, null)).ToList();

            Assert.Equal(2, result.Count);

            result = container.GetRoutedEndpoints(new MultiUserDataMessage(new[] { "user1", "user2" }, null)).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task TestEndpointsForDifferentContainersHaveDifferentStatus()
        {
            var endpoints = new[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "2"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "11"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "12")
            };

            var sem = new TestServiceEndpointManager(endpoints);

            var router = new TestEndpointRouter(false);
            var container1 = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestServiceConnection(ServiceConnectionStatus.Disconnected),
            }, e), sem, router, null);

            var container2 = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            var container3 = new MultiEndpointServiceConnectionContainer("hub-another",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            // Start the container for it to disconnect
            _ = container1.StartAsync();
            await container1.ConnectionInitializedTask.OrTimeout();

            // Start the container for it to disconnect
            _ = container2.StartAsync();
            await container2.ConnectionInitializedTask.OrTimeout();

            // Start the container for it to disconnect
            _ = container3.StartAsync();
            await container3.ConnectionInitializedTask.OrTimeout();

            var result = container1.GetRoutedEndpoints(new MultiGroupBroadcastDataMessage(new[] { "group1", "group2" }, null)).ToList();

            Assert.Equal(2, result.Count);

            result = container1.GetRoutedEndpoints(new MultiUserDataMessage(new[] { "user1", "user2" }, null)).ToList();

            Assert.Equal(2, result.Count);

            // The same hub shares the same endpoints
            result = container2.GetRoutedEndpoints(new MultiGroupBroadcastDataMessage(new[] { "group1", "group2" }, null)).ToList();

            Assert.Equal(2, result.Count);

            result = container2.GetRoutedEndpoints(new MultiUserDataMessage(new[] { "user1", "user2" }, null)).ToList();

            Assert.Equal(2, result.Count);

            // different hubs have different endpoint status
            result = container3.GetRoutedEndpoints(new MultiGroupBroadcastDataMessage(new[] { "group1", "group2" }, null)).ToList();

            Assert.Equal(2, result.Count);

            result = container3.GetRoutedEndpoints(new MultiUserDataMessage(new[] { "user1", "user2" }, null)).ToList();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task TestEndpointManagerWithDuplicateEndpoints()
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

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            _ = container.StartAsync();
            await container.ConnectionInitializedTask.OrTimeout();

            endpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Equal(2, endpoints.Length);
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
            var endpoints = sem.Endpoints.ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);

            var router = new TestEndpointRouter(false);
            var container = new MultiEndpointServiceConnectionContainer("hub",
                e => new TestBaseServiceConnectionContainer(new List<IServiceConnection> {
                new TestServiceConnection(),
                new TestServiceConnection(),
            }, e), sem, router, null);

            _ = container.StartAsync();
            await container.ConnectionInitializedTask.OrTimeout();

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
            var router = new TestEndpointRouter(true);
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

            _ = container.StartAsync();
            await container.ConnectionInitializedTask.OrTimeout();

            await container.WriteAsync(DefaultGroupMessage);
        }


        [Fact]
        public async Task TestContainerWithOneEndpointWithAllDisconnectedAndConnectionStartedThrows()
        {
            var sem = new TestServiceEndpointManager(new ServiceEndpoint(ConnectionString1));
            var router = new TestEndpointRouter(true);
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

            _ = container.StartAsync();
            await container.ConnectionInitializedTask.OrTimeout();

            await Assert.ThrowsAsync<ServiceConnectionNotActiveException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllConnectedFailsWithBadRouter()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1), 
                new ServiceEndpoint(ConnectionString2));

            var router = new TestEndpointRouter(true);
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

            _ = container.StartAsync();
            await container.ConnectionInitializedTask.OrTimeout();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => container.WriteAsync(DefaultGroupMessage)
                );
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithAllConnectedSucceedsWithGoodRouter()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2));

            var router = new TestEndpointRouter(false);
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

            _ = container.StartAsync();
            await container.ConnectionInitializedTask.OrTimeout();

            await container.WriteAsync(DefaultGroupMessage);
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

                var router = new TestEndpointRouter(false);
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

                _ = container.StartAsync();
                await container.ConnectionInitializedTask.OrTimeout();
                await container.WriteAsync(DefaultGroupMessage);
            }
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithOneOfflineSucceedsWithWarning()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, logChecker: logs =>
            {
                var warns = logs.Where(s => s.Write.LogLevel == LogLevel.Warning).ToList();
                Assert.Equal(1, warns.Count);
                Assert.Contains(warns, s => s.Write.Message.Contains("Message JoinGroupWithAckMessage is not sent to endpoint (Primary)http://url1 because all connections to this endpoint are offline."));
                return true;
            }))
            {
                var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

                var router = new TestEndpointRouter(false);
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
                await container.ConnectionInitializedTask.OrTimeout();
                await container.WriteAsync(DefaultGroupMessage);
            }
        }

        [Fact]
        public async Task TestContainerWithTwoEndpointWithPrimaryOfflineAndConnectionStartedSucceeds()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "online"));

            var router = new TestEndpointRouter(false);
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
            }, sem, router, null);

            _ = container.StartAsync();

            await container.WriteAsync(DefaultGroupMessage);

            var endpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Single(endpoints);

            Assert.Equal("online", endpoints.First().Name);
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
            private readonly bool _broken;

            public TestEndpointRouter(bool broken) : base()
            {
                _broken = broken;
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return base.GetEndpointsForBroadcast(endpoints);
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return base.GetEndpointsForConnection(connectionId, endpoints);
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return base.GetEndpointsForGroup(groupName, endpoints);
            }

            public override IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return base.GetEndpointsForUser(userId, endpoints);
            }

            public override ServiceEndpoint GetNegotiateEndpoint(IOwinContext context, IEnumerable<ServiceEndpoint> endpoints)
            {
                if (_broken)
                {
                    throw new InvalidOperationException();
                }

                return base.GetNegotiateEndpoint(context, endpoints);
            }
        }
    }
}
