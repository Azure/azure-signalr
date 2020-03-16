// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    public class TestEndpointServiceConnectionContainerTests : VerifiableLoggedTest
    {
        private const string ConnectionStringFormatter = "Endpoint={0};AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string Url1 = "http://url1";
        private const string Url2 = "https://url2";
        private const string Url3 = "http://url3";
        private readonly string ConnectionString1 = string.Format(ConnectionStringFormatter, Url1);
        private readonly string ConnectionString2 = string.Format(ConnectionStringFormatter, Url2);
        private readonly string ConnectionString3 = string.Format(ConnectionStringFormatter, Url3);
        private static readonly JoinGroupWithAckMessage DefaultGroupMessage = new JoinGroupWithAckMessage("a", "a", -1);

        public TestEndpointServiceConnectionContainerTests(ITestOutputHelper output) : base(output)
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
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
            }, e), sem, router, NullLoggerFactory.Instance);

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
            var endpoints = sem.Endpoints.Keys.OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);

            var router = new TestEndpointRouter();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
            }, e), sem, router, NullLoggerFactory.Instance);

            var containerEndpoints = container.GetOnlineEndpoints();
            Assert.Equal(2, containerEndpoints.Count());
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
            var endpoints = sem.Endpoints.Keys.OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);

            var router = new TestEndpointRouter();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
            }, e), sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            endpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("11", endpoints[1].Name);
        }

        [Fact]
        public void TestContainerWithNoEndpointThrowNoEndpointException()
        {
            Assert.Throws<AzureSignalRConfigurationNoEndpointException>(() => new TestServiceEndpointManager());
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
            var router = new TestEndpointRouter();

            var writeTcs = new TaskCompletionSource<object>();
            TestServiceConnectionContainer innerContainer = null;
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => innerContainer = new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
            }, e), sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            innerContainer.HandleAck(new AckMessage(1, (int)AckStatus.Ok));
            await task.OrTimeout();
        }

        [Fact]
        public async Task TestContainerWithOneEndpointWithAllDisconnectedConnectionThrows()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, logChecker: logs =>
            {
                var warns = logs.Where(s => s.Write.LogLevel == LogLevel.Warning).ToList();
                Assert.Single(warns);
                Assert.Contains(warns, s => s.Write.Message.Contains("Message JoinGroupWithAckMessage is not sent to endpoint (Primary)http://url1 because all connections to this endpoint are offline."));
                return true;
            }))
            { 
                var sem = new TestServiceEndpointManager(new ServiceEndpoint(ConnectionString1));
                var router = new DefaultEndpointRouter();
            
                var container = new TestMultiEndpointServiceConnectionContainer("hub",
                    e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                    new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                    new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                    new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                    new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                    new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                    new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                    new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                }, e), sem, router, loggerFactory);

                // All the connections started
                _ = container.StartAsync();
                await container.ConnectionInitializedTask.OrTimeout();

                await container.WriteAsync(DefaultGroupMessage);
            }
        }

        [Fact]
        public async Task TestContainerWithBadRouterThrows()
        {
            var sem = new TestServiceEndpointManager(new ServiceEndpoint(ConnectionString1));
            var throws = new InvalidOperationException();
            var router = new TestEndpointRouter(throws);
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
            }, e), sem, router, NullLoggerFactory.Instance);

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

            var router = new TestEndpointRouter(new InvalidOperationException());
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
                new TestSimpleServiceConnection(),
            }, e), sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

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

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
            }, e), sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            containers.First().Value.HandleAck(new AckMessage(1, (int)AckStatus.Ok));
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
                var container = new TestMultiEndpointServiceConnectionContainer("hub",
                    e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
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
                var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
                {
                    return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
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
                var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
                var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
                {
                    if (string.IsNullOrEmpty(e.Name))
                    {
                        return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        }, e);
                    }
                    return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        }, e);
                }, sem, router, loggerFactory);

                _ = container.StartAsync();
                var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
                await writeTcs.Task.OrTimeout();
                containers.First(p => !string.IsNullOrEmpty(p.Key.Name)).Value.HandleAck(new AckMessage(1, (int)AckStatus.Ok));
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
            var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected, writeAsyncTcs: writeTcs),
                    }, e);
                }
                return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    }, e);
            }, sem, router, NullLoggerFactory.Instance);

            _ = container.StartAsync();

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            containers.First(p => !string.IsNullOrEmpty(p.Key.Name)).Value.HandleAck(new AckMessage(1, (int)AckStatus.Ok));
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
                var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
                {
                    if (string.IsNullOrEmpty(e.Name))
                    {
                        return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                        new TestSimpleServiceConnection(ServiceConnectionStatus.Disconnected),
                    }, e);
                    }
                    return new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(),
                        new TestSimpleServiceConnection(),
                        new TestSimpleServiceConnection(),
                        new TestSimpleServiceConnection(),
                        new TestSimpleServiceConnection(),
                        new TestSimpleServiceConnection(),
                        new TestSimpleServiceConnection(),
                    }, e);
                }, sem, router, loggerFactory);

                _ = container.StartAsync();

                await container.WriteAsync(DefaultGroupMessage);
            }
        }

        [Fact]
        public async Task TestTwoEndpointsWithAllNotFoundAck()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    }, e);
                }
                return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                }, e);
            }, sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            foreach (var c in containers)
            {
                c.Value.HandleAck(new AckMessage(1, (int)AckStatus.NotFound));
            }
            var result = await task.OrTimeout();
            Assert.False(result);
        }

        [Fact]
        public async Task TestTwoEndpointsWithAllTimeoutAck()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    }, e);
                }
                return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                }, e);
            }, sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            foreach (var c in containers)
            {
                c.Value.HandleAck(new AckMessage(1, (int)AckStatus.Timeout));
            }
            await Assert.ThrowsAnyAsync<TimeoutException>(async () => await task.OrTimeout());
        }

        [Fact]
        public async Task TestTwoEndpointsWithoutAck()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    }, e, new AckHandler(100, 200));
                }
                return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                }, e, new AckHandler(100, 200));
            }, sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            foreach (var c in containers)
            {
                c.Value.HandleAck(new AckMessage(1, (int)AckStatus.Timeout));
            }
            await Assert.ThrowsAnyAsync<TimeoutException>(async () => await task).OrTimeout();
        }

        [Fact]
        public async Task TestTwoEndpointsWithOneSucceededAndOtherNotAcked()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    }, e, new AckHandler(100, 1000));
                }
                return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                }, e, new AckHandler(100, 1000));
            }, sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            containers.First().Value.HandleAck(new AckMessage(1, (int)AckStatus.Ok));
            var result = await task.OrTimeout();
            Assert.True(result);
        }

        [Fact]
        public async Task TestTwoEndpointsWithCancellationToken()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2, name: "online"));

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                        new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    }, e, new AckHandler(100, 200));
                }
                return containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                    new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                }, e, new AckHandler(100, 200));
            }, sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage, new CancellationToken(true));
            await writeTcs.Task.OrTimeout();
            foreach (var c in containers)
            {
                c.Value.HandleAck(new AckMessage(1, (int)AckStatus.Timeout));
            }
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task).OrTimeout();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestSingleEndpointOffline(bool migratable)
        {
            var manager = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1)
            );
            await TestEndpointOfflineInner(manager, new TestEndpointRouter(), migratable);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task TestMultiEndpointOffline(bool migratable)
        {
            var manager = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2)
            );
            await TestEndpointOfflineInner(manager, new TestEndpointRouter(), migratable);
        }

        [Fact]
        public async Task TestMultipleEndpointWithRenamesAndWriteAckableMessage()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "2"),
                new ServiceEndpoint(ConnectionString3, EndpointType.Secondary, "3")
                );

            var writeTcs = new TaskCompletionSource<object>();
            var endpoints = sem.Endpoints.Keys.OrderBy(x => x.Name).ToArray();
            Assert.Equal(3, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("2", endpoints[1].Name);
            Assert.Equal("3", endpoints[2].Name);

            var router = new TestEndpointRouter();
            var containers = new Dictionary<ServiceEndpoint, TestServiceConnectionContainer>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => containers[e] = new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs)
            }, e), sem, router, NullLoggerFactory.Instance);

            // All the connections started
            _ = container.StartAsync();
            await container.ConnectionInitializedTask;

            var containerEps = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            var container1 = containerEps[0].ConnectionContainer;
            Assert.Equal(3, containerEps.Length);
            Assert.Equal("1", containerEps[0].Name);
            Assert.Equal("2", containerEps[1].Name);
            Assert.Equal("3", containerEps[2].Name);

            // Trigger reload to test rename
            var renamedEndpoint = new ServiceEndpoint[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "11"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "2"),
                new ServiceEndpoint(ConnectionString3, EndpointType.Secondary, "33")
            };
            await sem.TestReloadServiceEndpoints(renamedEndpoint);

            // validate container level updates
            containerEps = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(3, containerEps.Length);
            Assert.Equal("11", containerEps[0].Name);
            // container1 keep same after rename
            Assert.Equal(container1, containerEps[0].ConnectionContainer);
            Assert.Equal("2", containerEps[1].Name);
            Assert.Equal("33", containerEps[2].Name);

            // validate sem negotiation endpoints updated
            var ngoEps = sem.GetEndpoints("hub").OrderBy(x => x.Name).ToArray();
            Assert.Equal(3, ngoEps.Length);
            Assert.Equal("11", ngoEps[0].Name);
            Assert.Equal("2", ngoEps[1].Name);
            Assert.Equal("33", ngoEps[2].Name);

            // write messages
            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            containers.First().Value.HandleAck(new AckMessage(1, (int)AckStatus.Ok));
            await task.OrTimeout();
        }

        [Theory]
        [MemberData(nameof(TestReloadEndpointsData))]
        public void TestServiceEndpointManagerReloadEndpoints(ServiceEndpoint[] oldValue, ServiceEndpoint[] newValue)
        {
            var sem = new TestServiceEndpointManager(oldValue);

            sem.TestReloadServiceEndpoints(newValue);

            var endpoints = sem.Endpoints.Keys;

            Assert.True(newValue.SequenceEqual(endpoints));
        }

        public static IEnumerable<object[]> TestReloadEndpointsData = new object[][]
        {
            // no change
            new object[]
            {
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "1"),
                    new ServiceEndpoint("Endpoint=http://url2;AccessKey=ABCDEFG", EndpointType.Primary, "2")
                },
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "1"),
                    new ServiceEndpoint("Endpoint=http://url2;AccessKey=ABCDEFG", EndpointType.Primary, "2")
                }
            },
            // add
            new object[]
            {
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "1")
                },
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "1"),
                    new ServiceEndpoint("Endpoint=http://url2;AccessKey=ABCDEFG", EndpointType.Primary, "2")
                }
            },
            // remove
            new object[]
            {
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "1"),
                    new ServiceEndpoint("Endpoint=http://url2;AccessKey=ABCDEFG", EndpointType.Primary, "2")
                },
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "1")
                }
            },
            // rename
            new object[]
            {
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "1"),
                    new ServiceEndpoint("Endpoint=http://url2;AccessKey=ABCDEFG", EndpointType.Primary, "2")
                },
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "22"),
                    new ServiceEndpoint("Endpoint=http://url2;AccessKey=ABCDEFG", EndpointType.Primary, "11")
                }
            },
            // type
            new object[]
            {
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Primary, "1"),
                    new ServiceEndpoint("Endpoint=http://url2;AccessKey=ABCDEFG", EndpointType.Secondary, "2")
                },
                new ServiceEndpoint[]
                {
                    new ServiceEndpoint("Endpoint=http://url1;AccessKey=ABCDEFG", EndpointType.Secondary, "1"),
                    new ServiceEndpoint("Endpoint=http://url2;AccessKey=ABCDEFG", EndpointType.Primary, "2")
                }
            }
        };

        private async Task TestEndpointOfflineInner(IServiceEndpointManager manager, IEndpointRouter router, bool migratable)
        {
            var containers = new List<TestServiceConnectionContainer>();

            var container = new TestMultiEndpointServiceConnectionContainer("hub", e =>
            {
                var c = new TestServiceConnectionContainer(new List<IServiceConnection>
                {
                    new TestSimpleServiceConnection(),
                    new TestSimpleServiceConnection()
                });
                c.MockOffline = true;
                containers.Add(c);
                return c;
            }, manager, router, NullLoggerFactory.Instance);

            foreach (var c in containers)
            {
                Assert.False(c.IsOffline);
            }

            var expected = container.OfflineAsync(migratable);
            var actual = await Task.WhenAny(
                expected,
                Task.Delay(TimeSpan.FromSeconds(1))
            );
            Assert.Equal(expected, actual);

            foreach (var c in containers)
            {
                Assert.True(c.IsOffline);
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

        private sealed class TestMultiEndpointServiceConnectionContainer : MultiEndpointServiceConnectionContainer
        {
            public TestMultiEndpointServiceConnectionContainer(string hub,
                                                          Func<HubServiceEndpoint, IServiceConnectionContainer> generator,
                                                          IServiceEndpointManager endpoint,
                                                          IEndpointRouter router,
                                                          ILoggerFactory loggerFactory

                ) : base(hub, generator, endpoint, router, loggerFactory)
            {
            }
        }

        private class TestServiceEndpointManager : ServiceEndpointManagerBase
        {
            private readonly ServiceEndpoint[] _endpoints;

            public TestServiceEndpointManager(params ServiceEndpoint[] endpoints) : base(endpoints, NullLogger.Instance)
            {
                _endpoints = endpoints;
            }

            public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
            {
                return null;
            }

            public Task TestReloadServiceEndpoints(ServiceEndpoint[] serviceEndpoints, int timeoutSec = 0)
            {
                return ReloadServiceEndpointsAsync(serviceEndpoints, TimeSpan.FromSeconds(timeoutSec));
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
