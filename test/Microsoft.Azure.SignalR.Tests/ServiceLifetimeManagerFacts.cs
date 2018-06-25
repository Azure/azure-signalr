// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceLifetimeManagerFacts
    {
        private static readonly List<string> TestUsers = new List<string> {"TestUser"};

        private static readonly List<string> TestGroups = new List<string> {"TestGroup"};

        private const string TestMethod = "TestMethod";

        private static readonly object[] TestArgs = new[] {"TestArgs"};

        private static readonly List<string> TestConnectionIds = new List<string> {"connectionId1"};

        [Theory]
        [InlineData("SendAllAsync", typeof(BroadcastDataMessage))]
        [InlineData("SendAllExceptAsync", typeof(BroadcastDataMessage))]
        [InlineData("SendConnectionsAsync", typeof(MultiConnectionDataMessage))]
        [InlineData("SendGroupAsync", typeof(GroupBroadcastDataMessage))]
        [InlineData("SendGroupsAsync", typeof(MultiGroupBroadcastDataMessage))]
        [InlineData("SendGroupExceptAsync", typeof(GroupBroadcastDataMessage))]
        [InlineData("SendUserAsync", typeof(UserDataMessage))]
        [InlineData("SendUsersAsync", typeof(MultiUserDataMessage))]
        [InlineData("AddToGroupAsync", typeof(JoinGroupMessage))]
        [InlineData("RemoveFromGroupAsync", typeof(LeaveGroupMessage))]
        public async void ServiceLifetimeManagerTest(string functionName, Type type)
        {
            var serviceConnectionManager = new TestServiceConnectionManager<TestHub>();
            var serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
                new TestClientConnectionManager(),
                new DefaultHubProtocolResolver(new IHubProtocol[] {new JsonHubProtocol(), new MessagePackHubProtocol()},
                    NullLogger<DefaultHubProtocolResolver>.Instance),
                NullLogger<ServiceLifetimeManager<TestHub>>.Instance);

            await CallFunction(serviceLifetimeManager, functionName);

            Assert.Equal(1, serviceConnectionManager.GetCallCount(type));
            VerifyMessageFileds(serviceConnectionManager.ServiceMessage, functionName);
        }

        [Theory]
        [InlineData("SendAllAsync", typeof(BroadcastDataMessage))]
        [InlineData("SendAllExceptAsync", typeof(BroadcastDataMessage))]
        [InlineData("SendConnectionAsync", typeof(MultiConnectionDataMessage))]
        [InlineData("SendConnectionsAsync", typeof(MultiConnectionDataMessage))]
        [InlineData("SendGroupAsync", typeof(GroupBroadcastDataMessage))]
        [InlineData("SendGroupsAsync", typeof(MultiGroupBroadcastDataMessage))]
        [InlineData("SendGroupExceptAsync", typeof(GroupBroadcastDataMessage))]
        [InlineData("SendUserAsync", typeof(UserDataMessage))]
        [InlineData("SendUsersAsync", typeof(MultiUserDataMessage))]
        [InlineData("AddToGroupAsync", typeof(JoinGroupMessage))]
        [InlineData("RemoveFromGroupAsync", typeof(LeaveGroupMessage))]
        public async void ServiceLifetimeManagerIntegrationTest(string functionName, Type type)
        {
            var proxy = new ServiceConnectionProxy();

            var serviceConnectionManager = new ServiceConnectionManager<TestHub>();
            serviceConnectionManager.AddServiceConnection(proxy.ServiceConnection);

            var serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
                proxy.ClientConnectionManager,
                new DefaultHubProtocolResolver(new IHubProtocol[] {new JsonHubProtocol(), new MessagePackHubProtocol()},
                    NullLogger<DefaultHubProtocolResolver>.Instance),
                NullLogger<ServiceLifetimeManager<TestHub>>.Instance
            );

            await proxy.StartAsync().OrTimeout();

            var _ = proxy.ProcessIncomingAsync();

            var task = proxy.WaitForMessage(type);

            await CallFunction(serviceLifetimeManager, functionName);

            var message = await task.OrTimeout();

            VerifyMessageFileds(message, functionName);
        }

        private async Task CallFunction(ServiceLifetimeManager<TestHub> serviceLifetimeManager, string functionName)
        {
            switch (functionName)
            {
                case "SendAllAsync":
                    await serviceLifetimeManager.SendAllAsync(TestMethod, TestArgs);
                    break;
                case "SendAllExceptAsync":
                    await serviceLifetimeManager.SendAllExceptAsync(TestMethod, TestArgs, TestConnectionIds);
                    break;
                case "SendConnectionAsync":
                    await serviceLifetimeManager.SendConnectionAsync(TestConnectionIds[0], TestMethod, TestArgs);
                    break;
                case "SendConnectionsAsync":
                    await serviceLifetimeManager.SendConnectionsAsync(TestConnectionIds, TestMethod, TestArgs);
                    break;
                case "SendGroupAsync":
                    await serviceLifetimeManager.SendGroupAsync(TestGroups[0], TestMethod, TestArgs);
                    break;
                case "SendGroupsAsync":
                    await serviceLifetimeManager.SendGroupsAsync(TestGroups, TestMethod, TestArgs);
                    break;
                case "SendGroupExceptAsync":
                    await serviceLifetimeManager.SendGroupExceptAsync(TestGroups[0], TestMethod, TestArgs,
                        TestConnectionIds);
                    break;
                case "SendUserAsync":
                    await serviceLifetimeManager.SendUserAsync(TestUsers[0], TestMethod, TestArgs);
                    break;
                case "SendUsersAsync":
                    await serviceLifetimeManager.SendUsersAsync(TestUsers, TestMethod, TestArgs);
                    break;
                case "AddToGroupAsync":
                    await serviceLifetimeManager.AddToGroupAsync(TestConnectionIds[0], TestGroups[0]);
                    break;
                case "RemoveFromGroupAsync":
                    await serviceLifetimeManager.RemoveFromGroupAsync(TestConnectionIds[0], TestGroups[0]);
                    break;
                default:
                    break;
            }
        }

        private void VerifyMessageFileds(ServiceMessage serviceMessage, string functionName)
        {
            switch (functionName)
            {
                case "SendAllAsync":
                    Assert.Null(((BroadcastDataMessage) serviceMessage).ExcludedList);
                    break;
                case "SendAllExceptAsync":
                    Assert.Equal(TestConnectionIds, ((BroadcastDataMessage) serviceMessage).ExcludedList);
                    break;
                case "SendConnectionAsync":
                    Assert.Equal(TestConnectionIds[0], ((MultiConnectionDataMessage) serviceMessage).ConnectionList[0]);
                    break;
                case "SendConnectionsAsync":
                    Assert.Equal(TestConnectionIds, ((MultiConnectionDataMessage) serviceMessage).ConnectionList);
                    break;
                case "SendGroupAsync":
                    Assert.Equal(TestGroups[0], ((GroupBroadcastDataMessage) serviceMessage).GroupName);
                    Assert.Null(((GroupBroadcastDataMessage) serviceMessage).ExcludedList);
                    break;
                case "SendGroupsAsync":
                    Assert.Equal(TestGroups, ((MultiGroupBroadcastDataMessage) serviceMessage).GroupList);
                    break;
                case "SendGroupExceptAsync":
                    Assert.Equal(TestGroups[0], ((GroupBroadcastDataMessage) serviceMessage).GroupName);
                    Assert.Equal(TestConnectionIds, ((GroupBroadcastDataMessage) serviceMessage).ExcludedList);
                    break;
                case "SendUserAsync":
                    Assert.Equal(TestUsers[0], ((UserDataMessage) serviceMessage).UserId);
                    break;
                case "SendUsersAsync":
                    Assert.Equal(TestUsers, ((MultiUserDataMessage) serviceMessage).UserList);
                    break;
                case "AddToGroupAsync":
                    Assert.Equal(TestConnectionIds[0], ((JoinGroupMessage) serviceMessage).ConnectionId);
                    Assert.Equal(TestGroups[0], ((JoinGroupMessage) serviceMessage).GroupName);
                    break;
                case "RemoveFromGroupAsync":
                    Assert.Equal(TestConnectionIds[0], ((LeaveGroupMessage) serviceMessage).ConnectionId);
                    Assert.Equal(TestGroups[0], ((LeaveGroupMessage) serviceMessage).GroupName);
                    break;
                default:
                    break;
            }
        }
    }
}