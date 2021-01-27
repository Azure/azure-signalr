// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Azure.SignalR.Tests.ServiceConnectionTests;

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

        [Fact(Skip = "Enable when custom interval is supported")]
        public async Task TestStatusPingChangesEndpointStatus()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Trace))
            {
                var endpoints = new[]
                {
                    new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "strong"),
                    new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "weak"),
                };

                var sem = new TestServiceEndpointManager(endpoints);

                var router = new TestEndpointRouter();

                var connectionFactory1 = new TestServiceConnectionFactory();
                var connectionFactory2 = new TestServiceConnectionFactory();

                var hub1 = new MultiEndpointServiceConnectionContainer(connectionFactory1, "hub1", 2, sem, router,
                    loggerFactory);
                var hub2 = new MultiEndpointServiceConnectionContainer(connectionFactory2, "hub2", 2, sem, router,
                    loggerFactory);

                var connections = connectionFactory1.CreatedConnections.ToArray();
                Assert.Equal(4, connections.Length);

                var connection1 = connections[0] as TestServiceConnection;
                var connection2 = connections[2] as TestServiceConnection;

                Assert.NotNull(connection1);
                Assert.NotNull(connection2);

                // All the connections started
                _ = hub1.StartAsync();
                await hub1.ConnectionInitializedTask;
                _ = hub2.StartAsync();
                await hub2.ConnectionInitializedTask;

                var protocol = new ServiceProtocol();
                for (var i = 0; i < 5; i++)
                {
                    protocol.WriteMessage(RuntimeServicePingMessage.GetStatusPingMessage(false), connection1.Application.Output);
                    await connection1.Application.Output.FlushAsync();
                    await Task.Delay(100);
                }

                await Task.Delay(100);

                var active = hub1.GetOnlineEndpoints().Where(s => s.IsActive).Count();
                Assert.Equal(1, active);

                active = hub2.GetOnlineEndpoints().Where(s => s.IsActive).Count();
                Assert.Equal(2, active);

                for (var i = 0; i < 5; i++)
                {
                    protocol.WriteMessage(RuntimeServicePingMessage.GetStatusPingMessage(false), connection2.Application.Output);
                    await connection2.Application.Output.FlushAsync();
                    await Task.Delay(100);
                }

                active = hub1.GetOnlineEndpoints().Where(s => s.IsActive).Count();
                Assert.Equal(0, active);

                // the original endpoints are not impacted
                active = sem.Endpoints.Where(s => s.Value.IsActive).Count();
                Assert.Equal(2, active);
            }
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
                Assert.Contains(warns, s => s.Write.Message.Contains(string.Format(MultiEndpointMessageWriter.Log.FailedWritingMessageToEndpointTemplate, "JoinGroupWithAckMessage", "(null)", "(Primary)http://url1")));
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
                Assert.Contains(warns, s => s.Write.Message.Contains(string.Format(MultiEndpointMessageWriter.Log.FailedWritingMessageToEndpointTemplate, "JoinGroupWithAckMessage", "(null)", "(Primary)http://url1")));
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
                Assert.Contains(warns, s => s.Write.Message.Contains(string.Format(MultiEndpointMessageWriter.Log.FailedWritingMessageToEndpointTemplate, "JoinGroupWithAckMessage", "(null)", "(Primary)http://url1")));
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
        [InlineData(GracefulShutdownMode.Off)]
        [InlineData(GracefulShutdownMode.WaitForClientsClose)]
        [InlineData(GracefulShutdownMode.MigrateClients)]
        internal async Task TestSingleEndpointOffline(GracefulShutdownMode mode)
        {
            var manager = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1)
            );
            await TestEndpointOfflineInner(manager, new TestEndpointRouter(), mode);
        }

        [Theory]
        [InlineData(GracefulShutdownMode.Off)]
        [InlineData(GracefulShutdownMode.WaitForClientsClose)]
        [InlineData(GracefulShutdownMode.MigrateClients)]
        internal async Task TestMultiEndpointOffline(GracefulShutdownMode mode)
        {
            var manager = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1),
                new ServiceEndpoint(ConnectionString2)
            );
            await TestEndpointOfflineInner(manager, new TestEndpointRouter(), mode);
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

        [Fact]
        public async Task TestEndpointManagerWithAddEndpointsWithTimeoutCanPromote()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1")
                );
            var endpoints = sem.Endpoints.Keys.ToArray();
            Assert.Single(endpoints);
            Assert.Equal("1", endpoints[0].Name);

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs)
            }, e), sem, router, NullLoggerFactory.Instance, TimeSpan.FromSeconds(10));

            var hubEndpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Single(hubEndpoints);
            Assert.Equal("1", hubEndpoints.First().Name);

            var newEndpoints = new ServiceEndpoint[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "2"),
                new ServiceEndpoint(ConnectionString3, EndpointType.Primary, "3")
            };
            var timeoutToken = new CancellationTokenSource(1000).Token;
            _ = sem.TestReloadServiceEndpoints(newEndpoints, 1);

            // wait timeout then check added successfully
            await Task.Delay(1100);

            // validate container side updated
            hubEndpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(3, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);
            Assert.Equal("2", hubEndpoints[1].Name);
            Assert.NotNull(hubEndpoints[1].ConnectionContainer);
            Assert.Equal("3", hubEndpoints[2].Name);
            Assert.NotNull(hubEndpoints[2].ConnectionContainer);

            // validate endpoint manager side update
            var ngoEps = sem.GetEndpoints("hub").OrderBy(x => x.Name).ToArray();
            Assert.Equal(3, ngoEps.Length);
            Assert.Equal("1", ngoEps[0].Name);
            Assert.Equal("2", ngoEps[1].Name);
            Assert.Equal("3", ngoEps[2].Name);
        }

        [Fact]
        public async Task TestEndpointManagerWithAddEndpointsWithServersTag()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1")
                );

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs)
            }, e), sem, router, NullLoggerFactory.Instance, TimeSpan.FromSeconds(10));

            var endpoints = sem.GetEndpoints("hub").ToArray();
            Assert.Single(endpoints);
            Assert.Equal("1", endpoints[0].Name);

            var hubEndpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Single(hubEndpoints);
            Assert.Equal("1", hubEndpoints.First().Name);

            var newEndpoints = new ServiceEndpoint[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "2")
            };
            _ = sem.TestReloadServiceEndpoints(newEndpoints, 10);

            // Wait a few time to let message router updated.
            await Task.Delay(100);

            hubEndpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);
            Assert.Equal("2", hubEndpoints[1].Name);

            // without Ready, still single endpoint available in negotiation
            var ngoEps = sem.GetEndpoints("hub").ToArray();
            Assert.Single(ngoEps);

            // Mock there're 3 servers SA,SB connected to EP1 and EP2
            var containers = container.GetTestOnlineContainers();
            var serversTag = "Server1;Server2;Server3";
            await Task.WhenAll(containers.Select(c => c.MockReceivedServersPing(serversTag)));

            // wait one interval+ for Ready state and check negotiation is added.
            await Task.Delay(6000);

            ngoEps = sem.GetEndpoints("hub").OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, ngoEps.Length);
            Assert.Equal("1", ngoEps[0].Name);
            Assert.Equal("2", ngoEps[1].Name);

            var task = container.WriteAckableMessageAsync(DefaultGroupMessage);
            await writeTcs.Task.OrTimeout();
            containers.First().HandleAck(new AckMessage(1, (int)AckStatus.Ok));
            await task.OrTimeout();
        }

        [Fact]
        public async Task TestEndpointManagerWithRemoveEndpointsWithNoClients()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "22")
                );
            var endpoints = sem.Endpoints.Keys.ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("22", endpoints[1].Name);

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs)
            }, e), sem, router, NullLoggerFactory.Instance);

            var hubEndpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);
            Assert.Equal("22", hubEndpoints[1].Name);

            // mock inactive
            var containers = container.GetTestOnlineContainers();
            await Task.WhenAll(containers.Select(x => x.MockReceivedStatusPing(false)));

            var newEndpoints = new ServiceEndpoint[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1")
            };
            _ = sem.TestReloadServiceEndpoints(newEndpoints, 1);

            // delay check
            await Task.Delay(100);

            // validate container side task completes
            hubEndpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Single(hubEndpoints);
            Assert.Equal("1", hubEndpoints[0].Name);

            // validate endpoint manager side update
            var ngoEps = sem.GetEndpoints("hub").ToArray();
            Assert.Single(ngoEps);
            Assert.Equal("1", ngoEps[0].Name);
        }

        [Fact]
        public async Task TestEndpointManagerWithRemovedEndpointsWithClients()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "22")
                );
            var endpoints = sem.Endpoints.Keys.ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("22", endpoints[1].Name);

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs)
            }, e), sem, router, NullLoggerFactory.Instance);

            var hubEndpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);
            Assert.Equal("22", hubEndpoints[1].Name);

            // mock all active to emulate has clients
            var containers = container.GetTestOnlineContainers();
            await Task.WhenAll(containers.Select(x => x.MockReceivedStatusPing(true)));

            var newEndpoints = new ServiceEndpoint[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1")
            };
            _ = sem.TestReloadServiceEndpoints(newEndpoints, 10);

            // validate container side not updated
            hubEndpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Equal(2, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);

            // validate endpoint manager side update
            var ngoEps = sem.GetEndpoints("hub").ToArray();
            Assert.Single(ngoEps);
            Assert.Equal("1", ngoEps[0].Name);

            // Mock client now drops and able to remove endpoints
            await Task.WhenAll(containers.Select(x => x.MockReceivedStatusPing(false)));
            await Task.Delay(6000);

            // validate container side updated
            hubEndpoints = container.GetOnlineEndpoints().ToArray();
            Assert.Single(hubEndpoints);
            Assert.Equal("1", hubEndpoints[0].Name);
        }

        [Fact]
        public async Task TestEndpointManagerWithMultiHubsWithEndpointTypeUpdate()
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary, "22")
                );
            var endpoints = sem.Endpoints.Keys.ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("22", endpoints[1].Name);

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs)
            }, e), sem, router, NullLoggerFactory.Instance);

            var container1 = new TestMultiEndpointServiceConnectionContainer("hub11",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs)
            }, e), sem, router, NullLoggerFactory.Instance);

            var hubEndpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);
            Assert.Equal("22", hubEndpoints[1].Name);

            // mock all active to emulate has clients
            var containers = container.GetTestOnlineContainers();
            await Task.WhenAll(containers.Select(x => x.MockReceivedStatusPing(true)));
            var containers1 = container1.GetTestOnlineContainers();
            await Task.WhenAll(containers1.Select(x => x.MockReceivedStatusPing(true)));

            var newEndpoints = new ServiceEndpoint[]
            {
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "22")
            };
            _ = sem.TestReloadServiceEndpoints(newEndpoints, 15);

            // validate container side update with news and have 3
            await Task.Delay(100);
            hubEndpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(3, hubEndpoints.Length);
            var expectedNames = new string[] { "1", "22", "22" };
            Assert.True(expectedNames.SequenceEqual(hubEndpoints.Select(e => e.Name).OrderBy(x => x)));
            var expectedTypes = new EndpointType[] { EndpointType.Primary, EndpointType.Primary, EndpointType.Secondary };
            Assert.True(expectedTypes.SequenceEqual(hubEndpoints.Select(e => e.EndpointType).OrderBy(x => x)));

            hubEndpoints = container1.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(3, hubEndpoints.Length);
            Assert.True(expectedNames.SequenceEqual(hubEndpoints.Select(e => e.Name).OrderBy(x => x)));
            Assert.True(expectedTypes.SequenceEqual(hubEndpoints.Select(e => e.EndpointType).OrderBy(x => x)));

            // validate endpoint manager side not updated yet
            var ngoEps = sem.GetEndpoints("hub").OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, ngoEps.Length);
            Assert.Equal("1", ngoEps[0].Name);
            Assert.Equal("22", ngoEps[1].Name);
            Assert.Equal(EndpointType.Secondary, ngoEps[1].EndpointType);

            // Mock add sync and validate negotiation side updated
            containers = container.GetTestOnlineContainers();
            await Task.WhenAll(containers.Select(x => x.MockReceivedServersPing("aaa;bbb")));
            containers1 = container1.GetTestOnlineContainers();
            await Task.WhenAll(containers1.Select(x => x.MockReceivedServersPing("aaa;bbb")));
            await Task.Delay(6000);

            ngoEps = sem.GetEndpoints("hub").OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, ngoEps.Length);
            Assert.Equal("1", ngoEps[0].Name);
            Assert.Equal("22", ngoEps[1].Name);
            Assert.Equal(EndpointType.Primary, ngoEps[1].EndpointType);

            // Mock status offlined
            containers = container.GetTestOnlineContainers();
            await Task.WhenAll(containers.Select(x => x.MockReceivedStatusPing(false)));
            containers1 = container1.GetTestOnlineContainers();
            await Task.WhenAll(containers1.Select(x => x.MockReceivedStatusPing(false)));
            await Task.Delay(6000);

            // validate container updated as well
            hubEndpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);
            Assert.Equal("22", hubEndpoints[1].Name);
            Assert.Equal(EndpointType.Primary, hubEndpoints[1].EndpointType);
            hubEndpoints = container1.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);
            Assert.Equal("22", hubEndpoints[1].Name);
            Assert.Equal(EndpointType.Primary, hubEndpoints[1].EndpointType);
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

        [Fact]
        public async Task ServiceConnectionContainerScopeWithPingUpdateTest()
        {
            using (StartVerifiableLog(out var loggerFactory))
            {
                // prepare containers
                var ccm = new TestClientConnectionManager();
                var ccf = new ClientConnectionFactory();
                var protocol = new ServiceProtocol();
                TestConnection transportConnection1 = null;
                TestConnection transportConnection2 = null;
                TestConnection transportConnection22 = null;
                var connectionFactory1 = new TestConnectionFactory(conn =>
                {
                    transportConnection1 = conn;
                    return Task.CompletedTask;
                });
                var connectionFactory2 = new TestConnectionFactory(conn =>
                {
                    transportConnection2 = conn;
                    return Task.CompletedTask;
                });
                var connectionFactory22 = new TestConnectionFactory(conn =>
                {
                    transportConnection22 = conn;
                    return Task.CompletedTask;
                });

                (var connectionHandler, var connectionDelegate) = GetConnectionDelegate();

                var ConnectionStringFormatter = "Endpoint={0};AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
                var Url1 = "http://url1";
                var Url2 = "http://url2";
                var Url22 = "http://url22";
                var ConnectionString1 = string.Format(ConnectionStringFormatter, Url1);
                var ConnectionString2 = string.Format(ConnectionStringFormatter, Url2);
                var ConnectionString22 = string.Format(ConnectionStringFormatter, Url22);

                var sem = new TestServiceEndpointManager(
                    new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                    new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "2")
                    );
                var endpoints = sem.GetEndpoints("hub");
                var connection1 = new ServiceConnection(protocol, ccm, connectionFactory1, loggerFactory, connectionDelegate, ccf,
                                    "serverId", "server-conn-1", endpoints[0], endpoints[0].ConnectionContainer as IServiceMessageHandler, closeTimeOutMilliseconds: 500);

                var connection2 = new ServiceConnection(protocol, ccm, connectionFactory2, loggerFactory, connectionDelegate, ccf,
                                    "serverId", "server-conn-2", endpoints[1], endpoints[1].ConnectionContainer as IServiceMessageHandler, closeTimeOutMilliseconds: 500);

                var connection22 = new ServiceConnection(protocol, ccm, connectionFactory22, loggerFactory, connectionDelegate, ccf,
                                    "serverId", "server-conn-22", endpoints[1], endpoints[1].ConnectionContainer as IServiceMessageHandler, closeTimeOutMilliseconds: 500);

                var router = new TestEndpointRouter();

                var multiContainer = new TestMultiEndpointServiceConnectionContainer("hub",
                    e =>
                    {
                        return e.Endpoint == endpoints[0].Endpoint ?
                         new TestServiceConnectionContainer(new List<IServiceConnection> { connection1 }, e) :
                        new TestServiceConnectionContainer(new List<IServiceConnection> { connection2, connection22 }, e);
                    }, sem, router, NullLoggerFactory.Instance);

                var containers = multiContainer.GetTestOnlineContainers();
                try
                {

                    await Task.Run(async () =>
                    {
                        _ = multiContainer.StartAsync();
                        await multiContainer.ConnectionInitializedTask;
                    });

                    // container 1
                    var taskCt1 = Task.Run(async () =>
                    {
                        // a client connected
                        await transportConnection1.Application.Output.WriteAsync(
                            protocol.GetMessageBytes(new OpenConnectionMessage("client1", null)));

                        _ = await ccm.WaitForClientConnectionAsync("client1").OrTimeout();

                        // server receives ping message 1
                        await containers[0].BaseHandlePingAsync(
                                new PingMessage { Messages = new[] { "d-m", "0" } });

                        await transportConnection1.Application.Output.WriteAsync(
                            protocol.GetMessageBytes(
                                new ConnectionDataMessage("client1", new ReadOnlySequence<byte>(new byte[] { 0x20 }))));
                        await Task.Delay(100);

                        // server receives ping message 2
                        await containers[0].BaseHandlePingAsync(
                                new PingMessage { Messages = new[] { "d-m", "1" } });

                        await transportConnection1.Application.Output.WriteAsync(
                            protocol.GetMessageBytes(
                                new ConnectionDataMessage("client1", new ReadOnlySequence<byte>(new byte[] { 0x20 }))));
                        await Task.Delay(100);
                    });

                    var tcs21 = new TaskCompletionSource<bool>();
                    var tcs22 = new TaskCompletionSource<bool>();
                    var tcs2Write = new TaskCompletionSource<bool>();

                    var taskCt2SendPings = Task.Run(async () =>
                    {
                        // clients connected
                        await transportConnection2.Application.Output.WriteAsync(
                            protocol.GetMessageBytes(new OpenConnectionMessage("client2", null)));

                        await transportConnection22.Application.Output.WriteAsync(
                            protocol.GetMessageBytes(new OpenConnectionMessage("client22", null)));

                        _ = await ccm.WaitForClientConnectionAsync("client2").OrTimeout();
                        _ = await ccm.WaitForClientConnectionAsync("client22").OrTimeout();


                        // server receives ping message 1
                        await containers[1].BaseHandlePingAsync(
                                new PingMessage { Messages = new[] { "d-m", "1" } });
                        tcs21.SetResult(true);

                        // server receives ping message 2
                        await tcs2Write.Task;
                        await containers[1].BaseHandlePingAsync(
                                new PingMessage { Messages = new[] { "d-m", "0" } });
                        tcs22.SetResult(true);
                    });

                    var taskCt2ReceivePings = Task.Run(async () =>
                    {
                        await tcs21.Task;
                        await transportConnection2.Application.Output.WriteAsync(
                            protocol.GetMessageBytes(
                                new ConnectionDataMessage("client2", new ReadOnlySequence<byte>(new byte[] { 0x20, 0x20 }))));
                        await transportConnection22.Application.Output.WriteAsync(
                            protocol.GetMessageBytes(
                                new ConnectionDataMessage("client22", new ReadOnlySequence<byte>(new byte[] { 0x20, 0x20, 0x20 }))));
                        await Task.Delay(100);
                        tcs2Write.SetResult(true);

                        await tcs22.Task;
                        await transportConnection2.Application.Output.WriteAsync(
                            protocol.GetMessageBytes(
                                new ConnectionDataMessage("client2", new ReadOnlySequence<byte>(new byte[] { 0x20, 0x20 }))));
                        await transportConnection22.Application.Output.WriteAsync(
                             protocol.GetMessageBytes(
                                 new ConnectionDataMessage("client22", new ReadOnlySequence<byte>(new byte[] { 0x20, 0x20, 0x20 }))));
                        await Task.Delay(100);
                    });

                    await Task.WhenAll(taskCt1, taskCt2SendPings, taskCt2ReceivePings);
                }
                finally
                {
                    connectionHandler.Cts.Cancel();
                }

                Assert.Equal(3, connectionHandler.Result.Count);
                Assert.Equal(new List<bool> { false, true }, connectionHandler.Result["client1"]);
                Assert.Equal(new List<bool> { true, false }, connectionHandler.Result["client2"]);
                Assert.Equal(new List<bool> { true, false }, connectionHandler.Result["client22"]);
            }
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(200, 5)]
        [InlineData(5, 0)]
        public async Task TestEndpointWithStatusPingPlusClientCount(int c1, int c2)
        {
            var sem = new TestServiceEndpointManager(
                new ServiceEndpoint(ConnectionString1, EndpointType.Primary, "1"),
                new ServiceEndpoint(ConnectionString2, EndpointType.Primary, "22")
                );
            var endpoints = sem.Endpoints.Keys.ToArray();
            Assert.Equal(2, endpoints.Length);
            Assert.Equal("1", endpoints[0].Name);
            Assert.Equal("22", endpoints[1].Name);

            var router = new TestEndpointRouter();
            var writeTcs = new TaskCompletionSource<object>();
            var container = new TestMultiEndpointServiceConnectionContainer("hub",
                e => new TestServiceConnectionContainer(new List<IServiceConnection> {
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs),
                new TestSimpleServiceConnection(writeAsyncTcs: writeTcs)
            }, e), sem, router, NullLoggerFactory.Instance);

            var hubEndpoints = container.GetOnlineEndpoints().OrderBy(x => x.Name).ToArray();
            Assert.Equal(2, hubEndpoints.Length);
            Assert.Equal("1", hubEndpoints[0].Name);
            Assert.Equal("22", hubEndpoints[1].Name);

            // mock client count ping and validate they won't messed.
            var containers = container.GetTestOnlineContainers();
            var container1 = containers.Where(x => x.Endpoint.Name == "1").FirstOrDefault();
            var container2 = containers.Where(x => x.Endpoint.Name == "22").FirstOrDefault();
            await Task.WhenAll(container1.MockReceivedStatusPing(true, c1));
            await Task.WhenAll(container2.MockReceivedStatusPing(true, c2));

            Assert.Equal(c1, hubEndpoints[0].EndpointMetrics.ClientConnectionCount);
            Assert.Equal(c2, hubEndpoints[1].EndpointMetrics.ClientConnectionCount);
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

        private (PingConnectionHandler, ConnectionDelegate) GetConnectionDelegate()
        {
            var services = new ServiceCollection();
            var connectionHandler = new PingConnectionHandler();
            services.AddSingleton(connectionHandler);
            var builder = new ConnectionBuilder(services.BuildServiceProvider());
            builder.UseConnectionHandler<PingConnectionHandler>();
            return (connectionHandler, builder.Build());
        }

        private async Task TestEndpointOfflineInner(IServiceEndpointManager manager, IEndpointRouter router, GracefulShutdownMode mode)
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

            var expected = container.OfflineAsync(mode);
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

        private sealed class PingConnectionHandler : ConnectionHandler
        {
            public ConcurrentDictionary<string, IList<bool>> Result = new ConcurrentDictionary<string, IList<bool>>();
            public CancellationTokenSource Cts { get; } = new CancellationTokenSource();
            public PingConnectionHandler()
            {
            }

            public override async Task OnConnectedAsync(ConnectionContext connection)
            {
                _ = ReadMessagesAsync(connection);
                while (!Cts.IsCancellationRequested)
                {
                    await Task.Delay(200);
                }
            }
            private async Task ReadMessagesAsync(ConnectionContext connection)
            {
                while (!Cts.IsCancellationRequested)
                {
                    var result = await connection.Transport.Input.ReadAsync();
                    var buffer = result.Buffer;
                    if (result.IsCompleted || result.IsCanceled)
                    {
                        break;
                    }
                    else
                    {
                        Result.AddOrUpdate(connection.ConnectionId,
                            new List<bool> { ServiceConnectionContainerScope.EnableMessageLog }, (key, old) =>
                            {
                                old.Add(ServiceConnectionContainerScope.EnableMessageLog);
                                return old;
                            });
                    }
                    connection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                }
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
                                                          ILoggerFactory loggerFactory,
                                                          TimeSpan? scaleTimeout = null
                ) : base(hub, generator, endpoint, router, loggerFactory)
            {
            }


            public List<TestServiceConnectionContainer> GetTestOnlineContainers()
            {
                var endpoints = GetOnlineEndpoints();
                return endpoints.Select(e => e.ConnectionContainer as TestServiceConnectionContainer).ToList();
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
