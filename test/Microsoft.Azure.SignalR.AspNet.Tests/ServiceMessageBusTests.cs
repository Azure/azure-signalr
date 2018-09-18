// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class ServiceMessageBusTests
    {
        private const string AppName = nameof(ServiceMessageBusTests);

        [Fact]
        public async Task PublishInvalidMessageThrows()
        {
            var dr = GetDefaultResolver(new string[] { }, out _);
            using (var bus = new ServiceMessageBus(dr))
            {
                await Assert.ThrowsAsync<NotSupportedException>(() => bus.Publish("test", "key", "1"));
            }
        }

        [Fact]
        public async Task PublishMessageToNotExistHubThrows()
        {
            var dr = GetDefaultResolver(new string[] { }, out _);
            using (var bus = new ServiceMessageBus(dr))
            {
                await Assert.ThrowsAsync<KeyNotFoundException>(() => bus.Publish("test", "h-key", "1"));
            }
        }

        public static IEnumerable<object[]> BroadcastTestMessages => new object[][]
            {
                // hub connection "c1" gets this connection message
                new object[]
                {
                    "h-c1", "hello", new string[] { "h-c1", "h", "c1" }, new string[] { "c1" }
                },
                // hub connection "a.b" gets this connection message
                new object[]
                {
                    "h-a.b", "hello", new string[] { "h-c1", "h", "a.b" }, new string[] { "a.b" },
                }
            };

        [Theory]
        [MemberData(nameof(BroadcastTestMessages))]
        public async Task PublishBroadcastMessagesTest(string messageKey, string messageValue, string[] availableHubs, string[] expectedHubs)
        {
            var dr = GetDefaultResolver(availableHubs, out var scm);

            PrepareConnection(scm, out var result);

            using (var bus = new ServiceMessageBus(dr))
            {
                await bus.Publish("test", messageKey, messageValue);
            }

            Assert.Equal(expectedHubs.Length, result.Count);

            for (var i = 0; i < expectedHubs.Length; i++)
            {
                Assert.True(result.TryGetValue(expectedHubs[i], out var current));
                var message = current as BroadcastDataMessage;
                Assert.NotNull(message);
                Assert.Equal(messageValue, message.Payloads["json"].GetSingleFramePayload());
            }
        }

        public static IEnumerable<object[]> ConnectionDataTestMessages => new object[][]
            {
                // app connection gets this connection message
                new object[]
                {
                    "hc-hub1.c1", "hello", new string[] { "hc", "hc-", "hub1", "hub1.c", "hub1.c1" }, new string[] { AppName }, new string[] { "c1" }
                },
                // app connection gets this connection message
                new object[]
                {
                    "hc-hub1.bi.conn1", "hello", new string[] { "hc", "hc-hub1", "hub1", "hub1.bi", "hub1.bi.conn" }, new string[] { AppName }, new string[] { "conn1" }
                }
            };

        [Theory]
        [MemberData(nameof(ConnectionDataTestMessages))]
        public async Task PublishConnectionDataMessagesTest(string messageKey, string messageValue, string[] availableHubs, string[] expectedHubs, string[] expectedConnectionIds)
        {
            var dr = GetDefaultResolver(availableHubs, out var scm);

            PrepareConnection(scm, out var result);

            using (var bus = new ServiceMessageBus(dr))
            {
                await bus.Publish("test", messageKey, messageValue);
            }

            Assert.Equal(expectedHubs.Length, result.Count);
            Assert.Equal(expectedConnectionIds.Length, result.Count);

            for (var i = 0; i < expectedHubs.Length; i++)
            {
                Assert.True(result.TryGetValue(expectedHubs[i], out var current));
                var message = current as ConnectionDataMessage;
                Assert.NotNull(message);

                Assert.Equal(expectedConnectionIds[i], message.ConnectionId);
                Assert.Equal(messageValue, message.Payload.First.GetSingleFramePayload());
            }
        }

        public static IEnumerable<object[]> GroupBroadcastTestMessages => new object[][]
            {
                // app connection gets this connection message
                new object[]
                {
                    // For groups, group name as a whole is considered as the group name
                    "hg-h1.group1", "hello", new string[] { "hg-h1", "hg", "h1" }, new string[] { AppName }, new string[] { "hg-h1.group1" }
                },
                // app connection gets this connection message
                new object[]
                {
                    "hg-h1.a1.group1", "hello", new string[] { "hg-h1", "hg", "h1", "h1.a1" }, new string[] { AppName }, new string[] { "hg-h1.a1.group1" }
                }
            };

        [Theory]
        [MemberData(nameof(GroupBroadcastTestMessages))]
        public async Task PublishGroupBroadcastDataMessagesTest(string messageKey, string messageValue, string[] availableHubs, string[] expectedHubs, string[] expectedGroups)
        {
            var dr = GetDefaultResolver(availableHubs, out var scm);

            PrepareConnection(scm, out var result);

            using (var bus = new ServiceMessageBus(dr))
            {
                await bus.Publish("test", messageKey, messageValue);
            }

            Assert.Equal(expectedHubs.Length, result.Count);
            Assert.Equal(expectedGroups.Length, result.Count);

            for (var i = 0; i < expectedHubs.Length; i++)
            {
                Assert.True(result.TryGetValue(expectedHubs[i], out var current));
                var message = current as GroupBroadcastDataMessage;
                Assert.NotNull(message);
                Assert.Equal(message.GroupName, expectedGroups[i]);
                Assert.Equal(messageValue, message.Payloads["json"].GetSingleFramePayload());
            }
        }

        public static IEnumerable<object[]> UserDataTestMessages => new object[][]
            {
                // hub connection "hub1" gets this connection message
                new object[]
                {
                    "hu-hub1.user1", "hello", new string[] { "hu", "hu-", "hub1", "hub1.u", "hub1.user1" }, new string[] { "hub1" }, new string[] { "user1" }
                },
                // hub connection "hub1" & "hub1.bi" gets this connection message
                new object[]
                {
                    "hu-hub1.bi.user1", "hello", new string[] { "hu", "hu-hub1", "hub1", "hub1.bi", "hub1.bi.user" }, new string[] { "hub1", "hub1.bi" }, new string[] { "bi.user1", "user1" }
                }
            };

        [Theory]
        [MemberData(nameof(UserDataTestMessages))]
        public async Task PublishUserDataMessagesTest(string messageKey, string messageValue, string[] availableHubs, string[] expectedHubs, string[] expectedUsers)
        {
            var dr = GetDefaultResolver(availableHubs, out var scm);

            PrepareConnection(scm, out var result);

            using (var bus = new ServiceMessageBus(dr))
            {
                await bus.Publish("test", messageKey, messageValue);
            }

            Assert.Equal(expectedHubs.Length, result.Count);
            Assert.Equal(expectedUsers.Length, result.Count);

            for (var i = 0; i < expectedHubs.Length; i++)
            {
                Assert.True(result.TryGetValue(expectedHubs[i], out var current));
                var message = current as UserDataMessage;
                Assert.NotNull(message);

                Assert.Equal(expectedUsers[i], message.UserId);
                Assert.Equal(messageValue, message.Payloads["json"].GetSingleFramePayload());
            }
        }

        private static void PrepareConnection(IServiceConnectionManager scm, out SortedList<string, ServiceMessage> output)
        {
            var result = new SortedList<string, ServiceMessage>();
            scm.Initialize(hub => new TestServiceConnection(hub,
                    m =>
                    {
                        lock (result)
                        {
                            result.Add(hub, m.Item1);
                        }
                    }), 5);
            output = result;
        }

        private static IDependencyResolver GetDefaultResolver(IReadOnlyList<string> hubs, out IServiceConnectionManager scm)
        {
            var resolver = new DefaultDependencyResolver();
            resolver.Register(typeof(IServiceProtocol), () => new ServiceProtocol());
            var connectionManager = new ServiceConnectionManager(AppName, hubs);
            resolver.Register(typeof(IServiceConnectionManager), () => connectionManager);
            resolver.Register(typeof(IMessageParser), () => new SignalRMessageParser(hubs, resolver));
            scm = connectionManager;
            return resolver;
        }

        private sealed class TestServiceConnection : IServiceConnectionContainer
        {
            private readonly Action<(ServiceMessage, IServiceConnectionContainer)> _validator;

            public string HubName { get; }

            public TestServiceConnection(string name, Action<(ServiceMessage, IServiceConnectionContainer)> validator)
            {
                _validator = validator;
                HubName = name;
            }

            public Task StartAsync()
            {
                return Task.CompletedTask;
            }

            public Task WriteAsync(ServiceMessage serviceMessage)
            {
                _validator((serviceMessage, this));
                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                return Task.CompletedTask;
            }

            public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
            {
                return WriteAsync(serviceMessage);
            }
        }

        private static IServiceProtocol DefaultServiceProtocol = new ServiceProtocol();

        private static ReadOnlyMemory<byte> GenerateSingleFrameBuffer(ReadOnlyMemory<byte> inner)
        {
            var singleFrameMessage = new ConnectionDataMessage(string.Empty, inner);
            return DefaultServiceProtocol.GetMessageBytes(singleFrameMessage);
        }

        private static ReadOnlyMemory<byte> GenerateSingleFrameBuffer(string message)
        {
            var inner = Encoding.UTF8.GetBytes(message);
            var singleFrameMessage = new ConnectionDataMessage(string.Empty, inner);
            return DefaultServiceProtocol.GetMessageBytes(singleFrameMessage);
        }
    }
}