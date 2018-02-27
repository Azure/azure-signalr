// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class HubHostLifetimeManager<THub> : HubLifetimeManager<THub>
    {
        private long _nextInvocationId;
        private readonly HubConnectionList _connections = new HubConnectionList();
        private readonly HubGroupList _groups = new HubGroupList();

        public override Task AddGroupAsync(string connectionId, string groupName)
        {
            if (connectionId == null)
            {
                throw new ArgumentNullException(nameof(connectionId));
            }

            if (groupName == null)
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            var connection = _connections[connectionId];
            if (connection == null)
            {
                return Task.CompletedTask;
            }

            _groups.Add(connection, groupName);
            // Ask SignalR Service to do 'AddGroupAsync'
            var message = CreateInvocationMessage(nameof(AddGroupAsync), new object[0],
                action: nameof(AddGroupAsync),
                connectionId: connectionId,
                groupName: groupName);
            return connection.WriteAsync(message);
        }

        public override Task SendAllAsync(string methodName, object[] args)
        {
            // Send the message to SignalR Service through any existing connection.
            // Here, we choose the first connection.
            var connection = _connections.ElementAt(0);
            var message = CreateInvocationMessage(methodName, args, action: nameof(SendAllAsync));
            return connection.WriteAsync(message);
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            var connection = _connections.ElementAt(0);
            var message = CreateInvocationMessage(methodName, args,
                action: nameof(SendConnectionAsync),
                excludedId: excludedIds);
            return connection.WriteAsync(message);
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args)
        {
            if (connectionId == null)
            {
                throw new ArgumentNullException(nameof(connectionId));
            }

            var connection = _connections[connectionId];

            if (connection == null)
            {
                return Task.CompletedTask;
            }
            var message = CreateInvocationMessage(methodName, args,
                action: nameof(SendConnectionAsync),
                connectionId: connectionId);
            return connection.WriteAsync(message);
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args)
        {
            throw new NotImplementedException();
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args)
        {
            if (groupName == null)
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            var group = _groups[groupName];
            if (group == null) return Task.CompletedTask;

            var connection = group.Values.ElementAt(0);
            var message = CreateInvocationMessage(methodName, args,
                action: nameof(SendGroupAsync),
                groupName: groupName);
            return connection.WriteAsync(message);
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args)
        {
            throw new NotImplementedException();
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            throw new NotImplementedException();
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args)
        {
            throw new NotImplementedException();
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args)
        {
            throw new NotImplementedException();
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

        public override Task RemoveGroupAsync(string connectionId, string groupName)
        {
            if (connectionId == null)
            {
                throw new ArgumentNullException(nameof(connectionId));
            }

            if (groupName == null)
            {
                throw new ArgumentNullException(nameof(groupName));
            }

            var connection = _connections[connectionId];
            if (connection == null)
            {
                return Task.CompletedTask;
            }

            _groups.Remove(connectionId, groupName);
            // Ask SignalR Service to do 'RemoveGroupAsync'
            var message = CreateInvocationMessage(nameof(RemoveGroupAsync), new object[0],
                action: nameof(RemoveGroupAsync),
                connectionId: connectionId,
                groupName: groupName);
            return connection.WriteAsync(message);
        }

        private string GetInvocationId()
        {
            return Interlocked.Increment(ref _nextInvocationId).ToString();
        }

        private InvocationMessage CreateInvocationMessage(string methodName, object[] args, string action = null,
            string connectionId = null, string groupName = null, IReadOnlyList<string> excludedId = null)
        {
            var message = new InvocationMessage(GetInvocationId(), methodName, null, args);
            if (!string.IsNullOrEmpty(action))
            {
                message.AddAction(action);
            }
            if (!string.IsNullOrEmpty(connectionId))
            {
                message.AddConnectionId(connectionId);
            }
            if (!string.IsNullOrEmpty(groupName))
            {
                message.AddGroupName(groupName);
            }
            if (excludedId != null && excludedId.Any())
            {
                message.AddExcludedIds(excludedId);
            }
            return message;
        }
    }
}
