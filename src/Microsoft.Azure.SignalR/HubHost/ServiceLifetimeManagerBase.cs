// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceLifetimeManagerBase<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        protected const string NullOrEmptyStringErrorMessage = "Argument cannot be null or empty.";
        protected const string TtlOutOfRangeErrorMessage = "Ttl cannot be less than 0.";
        protected readonly IServiceConnectionManager<THub> ServiceConnectionContainer;
        protected ILogger Logger { get; set; }

        private readonly DefaultHubMessageSerializer _messageSerializer;
        private readonly IClientConnectionManager _clientConnectionManager;

        public ServiceLifetimeManagerBase(IServiceConnectionManager<THub> serviceConnectionManager, IHubProtocolResolver protocolResolver, IOptions<HubOptions> globalHubOptions, IOptions<HubOptions<THub>> hubOptions, IClientConnectionManager clientConnectionManager, ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ServiceConnectionContainer = serviceConnectionManager;
            _messageSerializer = new DefaultHubMessageSerializer(protocolResolver, globalHubOptions.Value.SupportedProtocols, hubOptions.Value.SupportedProtocols);
            _clientConnectionManager = clientConnectionManager;
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new BroadcastDataMessage(null, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToBroadcastMessage(Logger, message);
            return WriteAsync(message);
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new BroadcastDataMessage(excludedIds, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToBroadcastMessage(Logger, message);
            return WriteAsync(message);
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new MultiConnectionDataMessage(new[] { connectionId }, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToSendMessageToConnections(Logger, message);
            return WriteAsync(message);
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionIds))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionIds));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new MultiConnectionDataMessage(connectionIds, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToSendMessageToConnections(Logger, message);
            return WriteAsync(message);
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new GroupBroadcastDataMessage(groupName, null, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToBroadcastMessageToGroup(Logger, message);
            return WriteAsync(message);
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupNames))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupNames));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new MultiGroupBroadcastDataMessage(groupNames, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToBroadcastMessageToGroups(Logger, message);
            // Send this message from a random service connection because this message involves of multiple groups.
            // Unless we send message for each group one by one, we can not guarantee the message order for all groups.
            return WriteAsync(message);
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new GroupBroadcastDataMessage(groupName, excludedIds, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToBroadcastMessageToGroup(Logger, message);
            return WriteAsync(message);
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new UserDataMessage(userId, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToSendMessageToUser(Logger, message);
            return WriteAsync(message);
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args,
            CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(userIds))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userIds));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = new MultiUserDataMessage(userIds, SerializeAllProtocols(methodName, args)).WithTracingId();
            Log.StartToSendMessageToUsers(Logger, message);
            return WriteAsync(message);
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (IsInvalidArgument(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var message = new JoinGroupWithAckMessage(connectionId, groupName).WithTracingId();
            Log.StartToAddConnectionToGroup(Logger, message);
            return WriteAckableMessageAsync(message);
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (IsInvalidArgument(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var message = new LeaveGroupWithAckMessage(connectionId, groupName).WithTracingId();
            Log.StartToRemoveConnectionFromGroup(Logger, message);
            return WriteAckableMessageAsync(message);
        }

        protected Task WriteAsync<T>(T message) where T : ServiceMessage, IMessageWithTracingId =>
            WriteCoreAsync(message, m => ServiceConnectionContainer.WriteAsync(m));

        protected Task WriteAckableMessageAsync<T>(T message) where T : ServiceMessage, IMessageWithTracingId => 
            WriteCoreAsync(message, m => ServiceConnectionContainer.WriteAckableMessageAsync(m));

        protected static bool IsInvalidArgument(string value)
        {
            return string.IsNullOrEmpty(value);
        }

        protected static bool IsInvalidArgument(IReadOnlyList<object> list)
        {
            return list == null || list.Count == 0;
        }

        protected IDictionary<string, ReadOnlyMemory<byte>> SerializeAllProtocols(string method, object[] args)
        {
            var payloads = new Dictionary<string, ReadOnlyMemory<byte>>();
            var message = new InvocationMessage(method, args);
            var serializedHubMessages = _messageSerializer.SerializeMessage(message);
            foreach (var serializedMessage in serializedHubMessages)
            {
                payloads.Add(serializedMessage.ProtocolName, serializedMessage.Serialized);
            }
            return payloads;
        }

        private async Task WriteCoreAsync<T>(T message, Func<T, Task> task) where T : ServiceMessage, IMessageWithTracingId
        {
            try
            {
                await task(message);
            }
            catch (TimeoutException ex)
            {
                // Regard the case connection already disconnected when send group message got timeout as success
                if (message is IAckableMessageWithConnectionId msg && !_clientConnectionManager.ClientConnections.TryGetValue(msg.ConnectionId, out var connection))
                {
                    Log.SucceededToSendMessage(Logger, message);
                    return;
                }

                Log.FailedToSendMessage(Logger, message, ex);
                throw;
            }
            catch (Exception ex)
            {
                Log.FailedToSendMessage(Logger, message, ex);
                throw;
            }
            Log.SucceededToSendMessage(Logger, message);
        }

        internal static class Log
        {
            public const string StartToBroadcastMessageTemplate = "Start to broadcast message {0}.";
            public const string StartToBroadcastMessageWithExcludedConnectionTemplate = "Start to broadcast message {0} except for {1} connections {2}.";
            public const string StartToSendMessageToConnectionsTemplate = "Start to send message {0} to {1} connections {2}.";
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

            public static void SucceededToSendMessage<T>(ILogger logger, T message) where T : ServiceMessage, IMessageWithTracingId
            {
                if (!Enabled())
                {
                    return;
                }

                _succeededToSendMessage(logger, message.TracingId, null);
            }

            public static void FailedToSendMessage<T>(ILogger logger, T message, Exception ex) where T : ServiceMessage, IMessageWithTracingId
            {
                if (!Enabled())
                {
                    return;
                }

                _failedToSendMessage(logger, message.TracingId, ex);
            }

            public static void StartToBroadcastMessage(ILogger logger, BroadcastDataMessage message)
            {
                if (!Enabled())
                {
                    return;
                }

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
                if (!Enabled())
                {
                    return;
                }
                var connections = string.Join(", ", message.ConnectionList);
                _startToSendMessageToConnections(logger, message.TracingId, message.ConnectionList.Count, connections, null);
            }

            public static void StartToBroadcastMessageToGroup(ILogger logger, GroupBroadcastDataMessage message)
            {
                if (!Enabled())
                {
                    return;
                }

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
                if (!Enabled())
                {
                    return;
                }
                var groups = string.Join(", ", message.GroupList);
                _startToBroadcastMessageToGroups(logger, message.TracingId, message.GroupList.Count, groups, null);
            }

            public static void StartToSendMessageToUser(ILogger logger, UserDataMessage message)
            {
                if (!Enabled())
                {
                    return;
                }
                _startToSendMessageToUser(logger, message.TracingId, message.UserId, null);
            }

            public static void StartToSendMessageToUsers(ILogger logger, MultiUserDataMessage message)
            {
                if (!Enabled())
                {
                    return;
                }
                var users = string.Join(", ", message.UserList);
                _startToSendMessageToUsers(logger, message.TracingId, message.UserList.Count, users, null);
            }

            public static void StartToAddConnectionToGroup(ILogger logger, JoinGroupWithAckMessage message)
            {
                if (!Enabled())
                {
                    return;
                }
                _startToAddConnectionToGroup(logger, message.TracingId, message.ConnectionId, message.GroupName, null);
            }

            public static void StartToRemoveConnectionFromGroup(ILogger logger, LeaveGroupWithAckMessage message)
            {
                if (!Enabled())
                {
                    return;
                }
                _startToRemoveConnectionFromGroup(logger, message.TracingId, message.ConnectionId, message.GroupName, null);
            }

            public static void StartToAddUserToGroup(ILogger logger, UserJoinGroupMessage message)
            {
                if (!Enabled())
                {
                    return;
                }
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
                if (!Enabled())
                {
                    return;
                }
                if (message.GroupName == null)
                {
                    _startToRemoveUserFromAllGroups(logger, message.TracingId, message.UserId, null);
                }
                else
                {
                    _startToRemoveUserFromGroup(logger, message.TracingId, message.UserId, message.GroupName, null);
                }
            }

            private static bool Enabled()
            {
                return ServiceConnectionContainerScope.EnableMessageLog || ClientConnectionScope.IsDiagnosticClient;
            }
        }
    }
}
