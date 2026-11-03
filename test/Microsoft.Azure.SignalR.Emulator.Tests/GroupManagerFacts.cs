// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Emulator.HubEmulator;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Microsoft.Azure.SignalR.Emulator.Tests
{
    public class GroupManagerFacts
    {
        private const string TestUser = "testUser";
        private const string TestGroup = "testGroup";

        [Fact]
        public async Task ExpiredUserGroupRemovesUserConnectionsFromGroup()
        {
            var connectionContext = new DefaultConnectionContext { ConnectionId = "testConnectionId" };
            var connection = new HubConnectionContext(connectionContext, new HubConnectionContextOptions(), NullLoggerFactory.Instance)
            {
                UserIdentifier = TestUser
            };

            var groupManager = new GroupManager();
            groupManager.OnConnectionOpenning(connection);
            groupManager.AddUserToGroup(TestUser, TestGroup, DateTimeOffset.Now);
            Assert.True(groupManager.GroupContainsConnections(TestGroup));

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (groupManager.GroupContainsUser(TestGroup, TestUser) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100);
            }

            Assert.False(groupManager.GroupContainsUser(TestGroup, TestUser));
            Assert.False(groupManager.GroupContainsConnections(TestGroup));
        }
    }
}
