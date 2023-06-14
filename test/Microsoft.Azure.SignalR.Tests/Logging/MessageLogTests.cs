// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class MessageLogTests
    {
        [Fact]
        public void MessageLogTest()
        {
            var logger = new TestLogger();
            using (var s = new ClientConnectionScope(null, null, true))
            {
                // broadcast
                MessageLog.StartToBroadcastMessage(logger, new BroadcastDataMessage(null, 123UL));
                Assert.Equal(string.Format(MessageLog.StartToBroadcastMessageTemplate, 123UL), logger.LogStr);

                MessageLog.StartToBroadcastMessage(logger, new BroadcastDataMessage(new[] { "x", "y" }, new Dictionary<string, ReadOnlyMemory<byte>>(), 123UL));
                Assert.Equal(string.Format(MessageLog.StartToBroadcastMessageWithExcludedConnectionTemplate, 123UL, 2, "x, y"), logger.LogStr);

                // send to connections
                MessageLog.StartToSendMessageToConnection(logger, new ConnectionDataMessage("id1", null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToSendMessageToConnectionTemplate, 123UL, "id1"), logger.LogStr);
                
                MessageLog.StartToSendMessageToConnections(logger, new MultiConnectionDataMessage(new[] { "id1", "id2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToSendMessageToConnectionsTemplate, 123UL, 2, "id1, id2"), logger.LogStr);

                // send to user/users
                MessageLog.StartToSendMessageToUser(logger, new UserDataMessage("user", null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToSendMessageToUserTemplate, 123UL, "user"), logger.LogStr);

                MessageLog.FailedToSendMessage(logger, new UserDataMessage("user", null, tracingId: 123UL), new Exception());
                Assert.Equal(string.Format(MessageLog.FailedToSendMessageTemplate, 123UL), logger.LogStr);

                MessageLog.SucceededToSendMessage(logger, new UserDataMessage("user", null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.SucceededToSendMessageTemplate, 123UL), logger.LogStr);
                
                MessageLog.ReceiveMessageFromService(logger, new ConnectionDataMessage("c", null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.ReceivedMessageFromClientConnectionTemplate, 123UL, "c"), logger.LogStr);
                
                MessageLog.StartToSendMessageToUsers(logger, new MultiUserDataMessage(new[] { "u1", "u2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToSendMessageToUsersTemplate, 123UL, 2, "u1, u2"), logger.LogStr);

                // send to group/groups
                MessageLog.StartToBroadcastMessageToGroup(logger, new GroupBroadcastDataMessage("g", null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToBroadcastMessageToGroupTemplate, 123UL, "g"), logger.LogStr);

                MessageLog.StartToBroadcastMessageToGroup(logger, new GroupBroadcastDataMessage("g", new[] { "c1", "c2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToBroadcastMessageToGroupWithExcludedConnectionsTemplate, 123UL, "g", 2, "c1, c2"), logger.LogStr);

                MessageLog.StartToBroadcastMessageToGroups(logger, new MultiGroupBroadcastDataMessage(new[] { "g1", "g2" }, null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToBroadcastMessageToGroupsTemplate, 123UL, 2, "g1, g2"), logger.LogStr);

                // connection join/leave group
                MessageLog.StartToAddConnectionToGroup(logger, new JoinGroupWithAckMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToAddConnectionToGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                MessageLog.StartToRemoveConnectionFromGroup(logger, new LeaveGroupWithAckMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToRemoveConnectionFromGroupTemplate, 123UL, "c", "g"), logger.LogStr);
                
                MessageLog.StartToCloseConnection(logger, new CloseConnectionMessage("c", "e") { TracingId = 123UL});
                Assert.Equal(string.Format(MessageLog.StartToSendMessageToCloseConnectionTemplate, 123UL, "c", "e"), logger.LogStr);
                
                MessageLog.StartToCheckIfConnectionExists(logger, new CheckConnectionExistenceWithAckMessage("c", 12, 123UL));
                Assert.Equal(string.Format(MessageLog.StartToSendMessageToCheckConnectionTemplate, 123UL, "c"), logger.LogStr);
                
                MessageLog.StartToCheckIfUserExists(logger, new CheckUserExistenceWithAckMessage("c", 12, 123UL));
                Assert.Equal(string.Format(MessageLog.StartToSendMessageToCheckIfUserExistsTemplate, 123UL, "c"), logger.LogStr);
                
                MessageLog.StartToCheckIfGroupExists(logger, new CheckGroupExistenceWithAckMessage("c", 12, 123UL));
                Assert.Equal(string.Format(MessageLog.StartToSendMessageToCheckIfGroupExistsTemplate, 123UL, "c"), logger.LogStr);
                
                // user join/leave group
                MessageLog.StartToAddUserToGroup(logger, new UserJoinGroupMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToAddUserToGroupTemplate, 123UL, "c", "g"), logger.LogStr);
                
                MessageLog.StartToCheckIfUserInGroup(logger, new CheckUserInGroupWithAckMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToCheckIfUserInGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                MessageLog.StartToRemoveUserFromGroup(logger, new UserLeaveGroupMessage("c", "g", tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToRemoveUserFromGroupTemplate, 123UL, "c", "g"), logger.LogStr);

                MessageLog.StartToRemoveUserFromGroup(logger, new UserLeaveGroupMessage("c", null, tracingId: 123UL));
                Assert.Equal(string.Format(MessageLog.StartToRemoveUserFromAllGroupsTemplate, 123UL, "c"), logger.LogStr);
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
