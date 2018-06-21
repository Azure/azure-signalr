// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceLifetimeManagerIT
    {
        private static readonly List<string> TestUsers = new List<string> { "TestUser" };

        private static readonly List<string> TestGroups = new List<string> { "TestGroup" };

        private static readonly string TestMethod = "TestMethod";

        private static readonly object[] TestArgs = new[] { "TestArgs" };

        private static readonly List<string> TestConnectionIds = new List<string> { "connectionId1" };

        private ServiceConnectionProxy Proxy;

        private ServiceLifetimeManager<TestHub> ServiceLifetimeManager;

        public ServiceLifetimeManagerIT()
        {
            Proxy = new ServiceConnectionProxy();

            var serviceConnectionManager = new ServiceConnectionManager<TestHub>();
            serviceConnectionManager.AddServiceConnection(Proxy.ServiceConnection);

            ServiceLifetimeManager = new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
                Proxy.ClientConnectionManager,
                new DefaultHubProtocolResolver(new IHubProtocol[] { new JsonHubProtocol(), new MessagePackHubProtocol() }, NullLogger<DefaultHubProtocolResolver>.Instance),
                NullLogger<ServiceLifetimeManager<TestHub>>.Instance
                );
        }

        [Fact]
        public async void SendAllAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(BroadcastDataMessage));

            await ServiceLifetimeManager.SendAllAsync(TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendAllExceptAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(BroadcastDataMessage));

            await ServiceLifetimeManager.SendAllExceptAsync(TestMethod, TestArgs, TestConnectionIds);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendConnectionAsync()
        {
            await Proxy.StartAsync().OrTimeout();
            var _ = Proxy.ProcessIncomingAsync();

            // Create a client connection
            var connection1Task = Proxy.WaitForConnectionAsync(TestConnectionIds[0]);

            await Proxy.WriteMessageAsync(new OpenConnectionMessage(TestConnectionIds[0], null));

            var connection1 = await connection1Task.OrTimeout();

            // Test when connection is on the server
            //Task task = Proxy.WaitForSpecificMessage(typeof(InvocationMessage));
            //await ServiceLifetimeManager.SendConnectionAsync(TestConnectionIds[0], TestMethod, TestArgs);
            //await task.OrTimeout();

            // Test when connection is not on the server
            string anotherConnectionId = "anotherConnectionId";
            Task task = Proxy.WaitForSpecificMessage(typeof(MultiConnectionDataMessage));
            await ServiceLifetimeManager.SendConnectionAsync(anotherConnectionId, TestMethod, TestArgs);
            await task.OrTimeout();
        }

        [Fact]
        public async void SendConnectionsAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(MultiConnectionDataMessage));

            await ServiceLifetimeManager.SendConnectionsAsync(TestConnectionIds, TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendGroupAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(GroupBroadcastDataMessage));

            await ServiceLifetimeManager.SendGroupAsync(TestGroups[0], TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendGroupsAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(MultiGroupBroadcastDataMessage));

            await ServiceLifetimeManager.SendGroupsAsync(TestGroups, TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendGroupExceptAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(GroupBroadcastDataMessage));

            await ServiceLifetimeManager.SendGroupExceptAsync(TestGroups[0], TestMethod, TestArgs, TestConnectionIds);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendUserAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(UserDataMessage));

            await ServiceLifetimeManager.SendUserAsync(TestUsers[0], TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendUsersAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(MultiUserDataMessage));

            await ServiceLifetimeManager.SendUsersAsync(TestUsers, TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void AddToGroupAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(JoinGroupMessage));

            await ServiceLifetimeManager.AddToGroupAsync(TestConnectionIds[0], TestGroups[0]);

            await task.OrTimeout();
        }

        [Fact]
        public async void RemoveFromGroupAsync()
        {
            await Proxy.StartAsync().OrTimeout();

            var _ = Proxy.ProcessIncomingAsync();

            Task task = Proxy.WaitForSpecificMessage(typeof(LeaveGroupMessage));

            await ServiceLifetimeManager.RemoveFromGroupAsync(TestConnectionIds[0], TestGroups[0]);

            await task.OrTimeout();
        }
    }
}
