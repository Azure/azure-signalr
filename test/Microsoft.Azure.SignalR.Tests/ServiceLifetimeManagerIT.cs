// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
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

        private readonly ServiceConnectionProxy _proxy;

        private readonly ServiceLifetimeManager<TestHub> _serviceLifetimeManager;

        public ServiceLifetimeManagerIT()
        {
            _proxy = new ServiceConnectionProxy();

            var serviceConnectionManager = new ServiceConnectionManager<TestHub>();
            serviceConnectionManager.AddServiceConnection(_proxy.ServiceConnection);

            _serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
                _proxy.ClientConnectionManager,
                new DefaultHubProtocolResolver(new IHubProtocol[] { new JsonHubProtocol(), new MessagePackHubProtocol() }, NullLogger<DefaultHubProtocolResolver>.Instance),
                NullLogger<ServiceLifetimeManager<TestHub>>.Instance
                );
        }

        [Fact]
        public async void SendAllAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(BroadcastDataMessage));

            await _serviceLifetimeManager.SendAllAsync(TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendAllExceptAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(BroadcastDataMessage));

            await _serviceLifetimeManager.SendAllExceptAsync(TestMethod, TestArgs, TestConnectionIds);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendConnectionAsync()
        {
            await _proxy.StartAsync().OrTimeout();
            var _ = _proxy.ProcessIncomingAsync();

            // Create a client connection
            var connection1Task = _proxy.WaitForConnectionAsync(TestConnectionIds[0]);

            await _proxy.WriteMessageAsync(new OpenConnectionMessage(TestConnectionIds[0], null));

            var connection1 = await connection1Task.OrTimeout();

            // Test when connection is not on the server
            string anotherConnectionId = "anotherConnectionId";
            Task task = _proxy.WaitForSpecificMessage(typeof(MultiConnectionDataMessage));
            await _serviceLifetimeManager.SendConnectionAsync(anotherConnectionId, TestMethod, TestArgs);
            await task.OrTimeout();
        }

        [Fact]
        public async void SendConnectionsAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(MultiConnectionDataMessage));

            await _serviceLifetimeManager.SendConnectionsAsync(TestConnectionIds, TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendGroupAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(GroupBroadcastDataMessage));

            await _serviceLifetimeManager.SendGroupAsync(TestGroups[0], TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendGroupsAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(MultiGroupBroadcastDataMessage));

            await _serviceLifetimeManager.SendGroupsAsync(TestGroups, TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendGroupExceptAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(GroupBroadcastDataMessage));

            await _serviceLifetimeManager.SendGroupExceptAsync(TestGroups[0], TestMethod, TestArgs, TestConnectionIds);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendUserAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(UserDataMessage));

            await _serviceLifetimeManager.SendUserAsync(TestUsers[0], TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void SendUsersAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(MultiUserDataMessage));

            await _serviceLifetimeManager.SendUsersAsync(TestUsers, TestMethod, TestArgs);

            await task.OrTimeout();
        }

        [Fact]
        public async void AddToGroupAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(JoinGroupMessage));

            await _serviceLifetimeManager.AddToGroupAsync(TestConnectionIds[0], TestGroups[0]);

            await task.OrTimeout();
        }

        [Fact]
        public async void RemoveFromGroupAsync()
        {
            await _proxy.StartAsync().OrTimeout();

            var _ = _proxy.ProcessIncomingAsync();

            Task task = _proxy.WaitForSpecificMessage(typeof(LeaveGroupMessage));

            await _serviceLifetimeManager.RemoveFromGroupAsync(TestConnectionIds[0], TestGroups[0]);

            await task.OrTimeout();
        }
    }
}
