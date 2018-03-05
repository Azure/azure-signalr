// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    public class HubHostLifetimeManager<THub> : HubLifetimeManager<THub>
    {
        private readonly HubConnectionList _connections = new HubConnectionList();
        private readonly HubGroupList _groups = new HubGroupList();
        private readonly ILogger<HubHostLifetimeManager<THub>> _logger;

        private long _nextInvocationId;
        private string InvocationId => Interlocked.Increment(ref _nextInvocationId).ToString();

        public HubHostLifetimeManager(ILogger<HubHostLifetimeManager<THub>> logger)
        {
            _logger = logger;
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            _connections.Add(connection);
            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            _connections.Remove(connection);
            return Task.CompletedTask;
        }

        public override Task SendAllAsync(string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var connection = _connections.FirstOrDefault();
            if (IsNullObject(connection, "No connection found to broadcast message.")) return Task.CompletedTask;

            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendAllAsync))
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var connection = _connections.FirstOrDefault();
            if (IsNullObject(connection, "No connection found to broadcast message.")) return Task.CompletedTask;

            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendAllExceptAsync))
                .WithExcludedIds(excludedIds)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var connection = _connections[connectionId];
            if (IsNullObject(connection, $"Connection[{connectionId}] not found.")) return Task.CompletedTask;

            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendConnectionAsync))
                .WithConnectionId(connectionId)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(connectionIds), connectionIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var connectionId = connectionIds.FirstOrDefault(id => _connections[id] != null);
            if (IsEmptyString(connectionId, "No valid connection found.")) return Task.CompletedTask;

            var connection = _connections[connectionId];
            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendConnectionsAsync))
                .WithConnectionIds(connectionIds)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var group = _groups[groupName];
            if (IsNullObject(group, $"Group[{groupName}] not found.")) return Task.CompletedTask;

            var connection = group.Values.FirstOrDefault();
            if (IsNullObject(connection, $"Group[{groupName}] is empty.")) return Task.CompletedTask;

            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendGroupAsync))
                .WithGroup(groupName)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(groupNames), groupNames)) return Task.CompletedTask;

            var groupName = groupNames.FirstOrDefault(group => _groups[group]?.Values.FirstOrDefault() != null);
            if (IsEmptyString(groupName, "No valid group found.")) return Task.CompletedTask;

            var connection = _groups[groupName].Values.First();
            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendGroupsAsync))
                .WithGroups(groupNames)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var group = _groups[groupName];
            if (IsNullObject(group, $"Group[{groupName}] not found.")) return Task.CompletedTask;

            var connection = group.Values.FirstOrDefault();
            if (IsNullObject(connection, $"Group[{groupName}] is empty.")) return Task.CompletedTask;

            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendGroupExceptAsync))
                .WithGroup(groupName)
                .WithExcludedIds(excludedIds)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(userId), userId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var connection = _connections.FirstOrDefault();
            if (IsNullObject(connection, "No connection found to send message.")) return Task.CompletedTask;

            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendUserAsync))
                .WithUser(userId)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(userIds), userIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var connection = _connections.FirstOrDefault();
            if (IsNullObject(connection, "No connection found to send message.")) return Task.CompletedTask;

            var message = new InvocationMessageBuilder(InvocationId, methodName, args)
                .WithAction(nameof(SendUsersAsync))
                .WithUsers(userIds)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task AddGroupAsync(string connectionId, string groupName)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            var connection = _connections[connectionId];
            if (IsNullObject(connection, $"Connection[{connectionId}] not found.")) return Task.CompletedTask;

            _groups.Add(connection, groupName);
            var message = new InvocationMessageBuilder(InvocationId, nameof(AddGroupAsync), new object[0])
                .WithAction(nameof(AddGroupAsync))
                .WithConnectionId(connectionId)
                .WithGroup(groupName)
                .Build();
            return connection.WriteAsync(message);
        }

        public override Task RemoveGroupAsync(string connectionId, string groupName)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            var connection = _connections[connectionId];
            if (IsNullObject(connection, $"Connection[{connectionId}] not found.")) return Task.CompletedTask;

            _groups.Remove(connectionId, groupName);
            var message = new InvocationMessageBuilder(InvocationId, nameof(RemoveGroupAsync), new object[0])
                .WithAction(nameof(RemoveGroupAsync))
                .WithConnectionId(connectionId)
                .WithGroup(groupName)
                .Build();
            return connection.WriteAsync(message);
        }

        private bool IsInvalidStringArgument(string name, string value)
        {
            return IsEmptyString(value, $"Null/empty string argument: {name}");
        }

        private bool IsInvalidListArgument(string name, IReadOnlyList<object> list)
        {
            if (list != null && list.Any()) return false;
            _logger.LogWarning($"Null/empty list argument: {name}");
            return true;
        }

        private bool IsEmptyString(string value, string message)
        {
            if (!string.IsNullOrEmpty(value)) return false;
            _logger.LogWarning(message);
            return true;
        }

        private bool IsNullObject(object value, string message)
        {
            if (value != null) return false;
            _logger.LogWarning(message);
            return true;
        }
    }
}
