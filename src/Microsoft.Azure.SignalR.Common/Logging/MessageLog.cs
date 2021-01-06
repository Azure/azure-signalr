// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.Azure.SignalR
{
    internal static class MessageLog
    {
        public const string StartToBroadcastMessageTemplate = "Start to broadcast message {0}.";
        public const string StartToBroadcastMessageWithExcludedConnectionTemplate = "Start to broadcast message {0} except for {1} connections {2}.";
        public const string StartToSendMessageToConnectionsTemplate = "Start to send message {0} to {1} connections {2}.";
        public const string StartToSendMessageToConnectionTemplate = "Start to send message {0} to connection {1}.";
        public const string StartToBroadcastMessageToGroupTemplate = "Start to broadcast message {0} to group {1}.";
        public const string StartToBroadcastMessageToGroupWithExcludedConnectionsTemplate = "Start to broadcast message {0} to group {1} except for {2} connections {3}.";
        public const string StartToBroadcastMessageToGroupsTemplate = "Start to broadcast message {0} to {1} groups {2}.";
        public const string StartToSendMessageToUserTemplate = "Start to send message {0} to user {1}.";
        public const string StartToSendMessageToUsersTemplate = "Start to send message {0} to {1} users {2}.";
        public const string StartToAddConnectionToGroupTemplate = "Start to send message {0} to add connection {1} to group {2}.";
        public const string StartToRemoveConnectionFromGroupTemplate = "Start to send message {0} to remove connection {1} from group {2}.";
        public const string StartToAddUserToGroupTemplate = "Start to send message {0} to add user {1} to group {2}.";
        public const string StartToAddUserToGroupWithTtlTemplate = "Start to send message {0} to add user {1} to group {2} with TTL {3} seconds.";
        public const string StartToRemoveUserFromGroupTemplate = "Start to send message {0} to remove user {1} from group {2}.";
        public const string StartToRemoveUserFromAllGroupsTemplate = "Start to send message {0} to remove user {1} from all groups.";
        public const string FailedToSendMessageTemplate = "Failed to send message {0}.";
        public const string SucceededToSendMessageTemplate = "Succeeded to send message {0}.";

        private static readonly Action<ILogger, ulong?, Exception> _startToBroadcastMessage =
            LoggerMessage.Define<ulong?>(
                LogLevel.Information,
                new EventId(0, "StartToBroadcastMessage"),
                StartToBroadcastMessageTemplate);

        private static readonly Action<ILogger, ulong?, int, string, Exception> _startToBroadcastMessageWithExcludedConnection =
            LoggerMessage.Define<ulong?, int, string>(
                LogLevel.Information,
                new EventId(1, "StartToBroadcastMessageWithExcludedConnection"),
                StartToBroadcastMessageWithExcludedConnectionTemplate);

        private static readonly Action<ILogger, ulong?, int, string, Exception> _startToSendMessageToConnections =
            LoggerMessage.Define<ulong?, int, string>(
                LogLevel.Information,
                new EventId(10, "StartToSendMessageToConnections"),
                StartToSendMessageToConnectionsTemplate);

        private static readonly Action<ILogger, ulong?, string, Exception> _startToSendMessageToConnection =
            LoggerMessage.Define<ulong?, string>(
                LogLevel.Information,
                new EventId(11, "StartToSendMessageToConnection"),
                StartToSendMessageToConnectionTemplate);

        private static readonly Action<ILogger, ulong?, string, Exception> _startToBroadcastMessageToGroup =
            LoggerMessage.Define<ulong?, string>(
                LogLevel.Information,
                new EventId(20, "StartToBroadcastMessageToGroup"),
                StartToBroadcastMessageToGroupTemplate);

        private static readonly Action<ILogger, ulong?, string, int, string, Exception> _startToBroadcastMessageToGroupWithExcludedConnections =
            LoggerMessage.Define<ulong?, string, int, string>(
                LogLevel.Information,
                new EventId(21, "StartToBroadcastMessageToGroupWithExcludedConnections"),
                StartToBroadcastMessageToGroupWithExcludedConnectionsTemplate);

        private static readonly Action<ILogger, ulong?, int, string, Exception> _startToBroadcastMessageToGroups =
            LoggerMessage.Define<ulong?, int, string>(
                LogLevel.Information,
                new EventId(30, "StartToBroadcastMessageToGroups"),
                StartToBroadcastMessageToGroupsTemplate);

        private static readonly Action<ILogger, ulong?, string, Exception> _startToSendMessageToUser =
            LoggerMessage.Define<ulong?, string>(
                LogLevel.Information,
                new EventId(40, "StartToSendMessageToUser"),
                StartToSendMessageToUserTemplate);

        private static readonly Action<ILogger, ulong?, int, string, Exception> _startToSendMessageToUsers =
            LoggerMessage.Define<ulong?, int, string>(
                LogLevel.Information,
                new EventId(50, "StartToSendMessageToUsers"),
                StartToSendMessageToUsersTemplate);

        private static readonly Action<ILogger, ulong?, string, string, Exception> _startToAddConnectionToGroup =
            LoggerMessage.Define<ulong?, string, string>(
                LogLevel.Information,
                new EventId(60, "StartToAddConnectionToGroup"),
                StartToAddConnectionToGroupTemplate);

        private static readonly Action<ILogger, ulong?, string, string, Exception> _startToRemoveConnectionFromGroup =
            LoggerMessage.Define<ulong?, string, string>(
                LogLevel.Information,
                new EventId(70, "StartToRemoveConnectionFromGroup"),
                StartToRemoveConnectionFromGroupTemplate);

        private static readonly Action<ILogger, ulong?, string, string, Exception> _startToAddUserToGroup =
            LoggerMessage.Define<ulong?, string, string>(
                LogLevel.Information,
                new EventId(80, "StartToAddUserToGroup"),
                StartToAddUserToGroupTemplate);

        private static readonly Action<ILogger, ulong?, string, string, int?, Exception> _startToAddUserToGroupWithTtl =
            LoggerMessage.Define<ulong?, string, string, int?>(
                LogLevel.Information,
                new EventId(81, "StartToAddUserToGroupWithTtl"),
                StartToAddUserToGroupWithTtlTemplate);

        private static readonly Action<ILogger, ulong?, string, string, Exception> _startToRemoveUserFromGroup =
            LoggerMessage.Define<ulong?, string, string>(
                LogLevel.Information,
                new EventId(90, "StartToRemoveUserFromGroup"),
                StartToRemoveUserFromGroupTemplate);

        private static readonly Action<ILogger, ulong?, string, Exception> _startToRemoveUserFromAllGroups =
            LoggerMessage.Define<ulong?, string>(
                LogLevel.Information,
                new EventId(91, "StartToRemoveUserFromAllGroups"),
                StartToRemoveUserFromAllGroupsTemplate);

        private static readonly Action<ILogger, ulong?, Exception> _failedToSendMessage =
            LoggerMessage.Define<ulong?>(
                LogLevel.Warning,
                new EventId(100, "FailedToSendMessage"),
                FailedToSendMessageTemplate);

        private static readonly Action<ILogger, ulong?, Exception> _succeededToSendMessage =
            LoggerMessage.Define<ulong?>(
                LogLevel.Information,
                new EventId(110, "SucceededToSendMessage"),
                SucceededToSendMessageTemplate);

        private static readonly Action<ILogger, ulong?, string, Exception> _receivedMessageFromService =
                LoggerMessage.Define<ulong?, string>(
                    LogLevel.Information,
                    new EventId(120, "RecieveMessageFromService"),
                    "Received message {tracingId} from client connection {connectionId}.");

        public static void ReceiveMessageFromService(ILogger logger, ConnectionDataMessage message)
        {
            _receivedMessageFromService(logger, message.TracingId, message.ConnectionId, null);
        }

        public static void SucceededToSendMessage(ILogger logger, IMessageWithTracingId message)
        {
            _succeededToSendMessage(logger, message.TracingId, null);
        }

        public static void FailedToSendMessage(ILogger logger, IMessageWithTracingId message, Exception ex)
        {
            _failedToSendMessage(logger, message.TracingId, ex);
        }

        public static void StartToBroadcastMessage(ILogger logger, BroadcastDataMessage message)
        {
            if (message.ExcludedList == null || message.ExcludedList.Count == 0)
            {
                _startToBroadcastMessage(logger, message.TracingId, null);
            }
            else
            {
                // todo: ? should we hide some by "..." if the excluded list is too long (max count: 20) - e.g. "excecpt for <list count> connections: connId1, connId2, ..."
                var excludedConnections = string.Join(", ", message.ExcludedList);
                _startToBroadcastMessageWithExcludedConnection(logger, message.TracingId, message.ExcludedList.Count, excludedConnections, null);
            }
        }

        public static void StartToSendMessageToConnections(ILogger logger, MultiConnectionDataMessage message)
        {
            var connections = string.Join(", ", message.ConnectionList);
            _startToSendMessageToConnections(logger, message.TracingId, message.ConnectionList.Count, connections, null);
        }

        public static void StartToSendMessageToConnection(ILogger logger, ConnectionDataMessage message)
        {
            _startToSendMessageToConnection(logger, message.TracingId, message.ConnectionId, null);
        }

        public static void StartToBroadcastMessageToGroup(ILogger logger, GroupBroadcastDataMessage message)
        {
            if (message.ExcludedList == null || message.ExcludedList.Count == 0)
            {
                _startToBroadcastMessageToGroup(logger, message.TracingId, message.GroupName, null);
            }
            else
            {
                var connections = string.Join(", ", message.ExcludedList);
                _startToBroadcastMessageToGroupWithExcludedConnections(logger, message.TracingId, message.GroupName, message.ExcludedList.Count, connections, null);
            }
        }

        public static void StartToBroadcastMessageToGroups(ILogger logger, MultiGroupBroadcastDataMessage message)
        {
            var groups = string.Join(", ", message.GroupList);
            _startToBroadcastMessageToGroups(logger, message.TracingId, message.GroupList.Count, groups, null);
        }

        public static void StartToSendMessageToUser(ILogger logger, UserDataMessage message)
        {
            _startToSendMessageToUser(logger, message.TracingId, message.UserId, null);
        }

        public static void StartToSendMessageToUsers(ILogger logger, MultiUserDataMessage message)
        {
            var users = string.Join(", ", message.UserList);
            _startToSendMessageToUsers(logger, message.TracingId, message.UserList.Count, users, null);
        }

        public static void StartToAddConnectionToGroup(ILogger logger, JoinGroupWithAckMessage message)
        {
            _startToAddConnectionToGroup(logger, message.TracingId, message.ConnectionId, message.GroupName, null);
        }

        public static void StartToRemoveConnectionFromGroup(ILogger logger, LeaveGroupWithAckMessage message)
        {
            _startToRemoveConnectionFromGroup(logger, message.TracingId, message.ConnectionId, message.GroupName, null);
        }

        public static void StartToAddUserToGroup(ILogger logger, UserJoinGroupMessage message)
        {
            if (message.Ttl == null)
            {
                _startToAddUserToGroup(logger, message.TracingId, message.UserId, message.GroupName, null);
            }
            else
            {
                _startToAddUserToGroupWithTtl(logger, message.TracingId, message.UserId, message.GroupName, message.Ttl, null);
            }
        }

        public static void StartToRemoveUserFromGroup(ILogger logger, UserLeaveGroupMessage message)
        {
            if (message.GroupName == null)
            {
                _startToRemoveUserFromAllGroups(logger, message.TracingId, message.UserId, null);
            }
            else
            {
                _startToRemoveUserFromGroup(logger, message.TracingId, message.UserId, message.GroupName, null);
            }
        }
    }
}
