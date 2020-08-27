// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class MessageLogTests
    {
        [Fact]
        public void MessageLogTest()
        {
            var logger = new TestLogger();
            using (var s = new ClientConnectionScope(null, true))
            {
                // broadcast
                ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessage(logger, new BroadcastDataMessage(null, 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessageTemplate, 123UL), logger.LogStr);

                ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessage(logger, new BroadcastDataMessage(new[] { "x", "y" }, new Dictionary<string, ReadOnlyMemory<byte>>(), 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessageWithExcludedConnectionTemplate, 123UL, 2, "x, y"), logger.LogStr);

                // send to connections
                ServiceLifetimeManagerBase<Hub>.Log.StartToSendMessageToConnections(logger, new MultiConnectionDataMessage(new[] { "id1", "id2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToSendMessageToConnectionsTemplate, 123UL, 2, "id1, id2"), logger.LogStr);

                // send to user/users
                ServiceLifetimeManagerBase<Hub>.Log.StartToSendMessageToUser(logger, new UserDataMessage("user", null, tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToSendMessageToUserTemplate, 123UL, "user"), logger.LogStr);

                ServiceLifetimeManagerBase<Hub>.Log.StartToSendMessageToUsers(logger, new MultiUserDataMessage(new[] { "u1", "u2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToSendMessageToUsersTemplate, 123UL, 2, "u1, u2"), logger.LogStr);

                // send to group/groups
                ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessageToGroup(logger, new GroupBroadcastDataMessage("g", null, tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessageToGroupTemplate, 123UL, "g"), logger.LogStr);

                ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessageToGroup(logger, new GroupBroadcastDataMessage("g", new[] { "c1", "c2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessageToGroupWithExcludedConnectionsTemplate, 123UL, "g", 2, "c1, c2"), logger.LogStr);

                ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessageToGroups(logger, new MultiGroupBroadcastDataMessage(new[] { "g1", "g2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToBroadcastMessageToGroupsTemplate, 123UL, 2, "g1, g2"), logger.LogStr);

                // connection join/leave group
                ServiceLifetimeManagerBase<Hub>.Log.StartToAddConnectionToGroup(logger, new JoinGroupWithAckMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToAddConnectionToGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                ServiceLifetimeManagerBase<Hub>.Log.StartToRemoveConnectionFromGroup(logger, new LeaveGroupWithAckMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToRemoveConnectionFromGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                // user join/leave group
                ServiceLifetimeManagerBase<Hub>.Log.StartToAddUserToGroup(logger, new UserJoinGroupMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToAddUserToGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                ServiceLifetimeManagerBase<Hub>.Log.StartToRemoveUserFromGroup(logger, new UserLeaveGroupMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToRemoveUserFromGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                ServiceLifetimeManagerBase<Hub>.Log.StartToRemoveUserFromGroup(logger, new UserLeaveGroupMessage("c", null, tracingId: 123UL));
                Assert.Equal(string.Format(ServiceLifetimeManagerBase<Hub>.Log.StartToRemoveUserFromAllGroupsTemplate, 123UL, "c"), logger.LogStr);
            }
        }

        private class TestLogger : ILogger
        {
            public string LogStr { get; private set; }

            public IDisposable BeginScope<TState>(TState state)
            {
                throw new NotImplementedException();
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                LogStr = formatter(state, exception);
            }
        }
    }
}
