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
                AzureSignalRLog.StartToBroadcastMessage(logger, new BroadcastDataMessage(null, 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToBroadcastMessageTemplate, 123UL), logger.LogStr);

                AzureSignalRLog.StartToBroadcastMessage(logger, new BroadcastDataMessage(new[] { "x", "y" }, new Dictionary<string, ReadOnlyMemory<byte>>(), 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToBroadcastMessageWithExcludedConnectionTemplate, 123UL, 2, "x, y"), logger.LogStr);

                // send to connections
                AzureSignalRLog.StartToSendMessageToConnections(logger, new MultiConnectionDataMessage(new[] { "id1", "id2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToSendMessageToConnectionsTemplate, 123UL, 2, "id1, id2"), logger.LogStr);

                // send to user/users
                AzureSignalRLog.StartToSendMessageToUser(logger, new UserDataMessage("user", null, tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToSendMessageToUserTemplate, 123UL, "user"), logger.LogStr);

                AzureSignalRLog.StartToSendMessageToUsers(logger, new MultiUserDataMessage(new[] { "u1", "u2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToSendMessageToUsersTemplate, 123UL, 2, "u1, u2"), logger.LogStr);

                // send to group/groups
                AzureSignalRLog.StartToBroadcastMessageToGroup(logger, new GroupBroadcastDataMessage("g", null, tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToBroadcastMessageToGroupTemplate, 123UL, "g"), logger.LogStr);

                AzureSignalRLog.StartToBroadcastMessageToGroup(logger, new GroupBroadcastDataMessage("g", new[] { "c1", "c2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToBroadcastMessageToGroupWithExcludedConnectionsTemplate, 123UL, "g", 2, "c1, c2"), logger.LogStr);

                AzureSignalRLog.StartToBroadcastMessageToGroups(logger, new MultiGroupBroadcastDataMessage(new[] { "g1", "g2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToBroadcastMessageToGroupsTemplate, 123UL, 2, "g1, g2"), logger.LogStr);

                // connection join/leave group
                AzureSignalRLog.StartToAddConnectionToGroup(logger, new JoinGroupWithAckMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToAddConnectionToGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                AzureSignalRLog.StartToRemoveConnectionFromGroup(logger, new LeaveGroupWithAckMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToRemoveConnectionFromGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                // user join/leave group
                AzureSignalRLog.StartToAddUserToGroup(logger, new UserJoinGroupMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToAddUserToGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                AzureSignalRLog.StartToRemoveUserFromGroup(logger, new UserLeaveGroupMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToRemoveUserFromGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                AzureSignalRLog.StartToRemoveUserFromGroup(logger, new UserLeaveGroupMessage("c", null, tracingId: 123UL));
                Assert.Equal(string.Format(AzureSignalRLog.StartToRemoveUserFromAllGroupsTemplate, 123UL, "c"), logger.LogStr);
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
