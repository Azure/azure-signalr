// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class HubHostLifetimeManager<THub> : CloudHubLifetimeManager<THub> where THub : Hub
    {
        private readonly HubConnectionStore _connections = new HubConnectionStore();
        private readonly HubGroupList _groups = new HubGroupList();

        public override Task AddGroupAsync(string connectionId, string groupName)
        {
            CheckNullConnectionId(connectionId);

            CheckNullGroup(groupName);

            var connection = _connections[connectionId];
            if (connection == null)
            {
                return Task.CompletedTask;
            }

            _groups.Add(connection, groupName);
            // Ask SignalR Service to do 'AddGroupAsync'
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(AddGroupAsync))
                .AddConnectionId(connectionId)
                .AddGroupName(groupName);
            return WriteRaw(connection, meta, null, null);
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
            CheckNullConnectionId(connectionId);

            CheckNullGroup(groupName);

            var connection = _connections[connectionId];
            if (connection == null)
            {
                // TODO. log an error that the client does not belong to the group
                return Task.CompletedTask;
            }

            _groups.Remove(connectionId, groupName);
            // Ask SignalR Service to do 'RemoveGroupAsync'
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(RemoveGroupAsync))
                .AddConnectionId(connectionId)
                .AddGroupName(groupName);
            return WriteRaw(connection, meta, null, null);
        }

        public override Task SendAllAsyncFromCloud(string methodName, object[] args, string cloudConnectionId)
        {
            HubConnectionContext connection = _connections[cloudConnectionId];
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendAllAsync));
            return WriteAllProtocolRaw(connection, meta, methodName, args);
        }

        public override Task SendAllExceptAsyncFromCloud(string methodName, object[] args, IReadOnlyList<string> excludedIds, string cloudConnectionId)
        {
            HubConnectionContext connection = _connections[cloudConnectionId];
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendAllExceptAsync))
                .AddExcludedIds(excludedIds);

            return WriteAllProtocolRaw(connection, meta, methodName, args);
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args)
        {
            CheckNullConnectionId(connectionId);

            var connection = _connections[connectionId];

            if (connection == null)
            {
                // TODO. log an error that the specified connectionId is not existed.
                return Task.CompletedTask;
            }
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendConnectionAsync))
                .AddConnectionId(connectionId);
            return WriteRaw(connection, meta, methodName, args);
        }

        public override Task SendConnectionsAsyncFromCloud(IReadOnlyList<string> connectionIds, string methodName, object[] args, string cloudConnectionId)
        {
            HubConnectionContext connection = _connections[cloudConnectionId];
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendConnectionsAsync))
                .AddConnectionIds(connectionIds);
            return WriteAllProtocolRaw(connection, meta, methodName, args);
        }

        public override Task SendGroupAsyncFromCloud(string groupName, string methodName, object[] args, string cloudConnectionId)
        {
            CheckNullGroup(groupName);

            var group = _groups[groupName];
            if (group != null)
            {
                HubConnectionContext connection = _connections[cloudConnectionId];
                var meta = new MessageMetaDataDictionary();
                meta.AddAction(nameof(SendGroupAsync))
                    .AddGroupName(groupName);
                return WriteAllProtocolRaw(connection, meta, methodName, args);
            }
            else
            {
                // TODO. log an error if specified a wrong group name
            }

            return Task.CompletedTask;
        }

        public override Task SendGroupExceptAsyncFromCloud(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds, string cloudConnectionId)
        {
            CheckNullGroup(groupName);

            var group = _groups[groupName];
            if (group != null)
            {
                HubConnectionContext connection = _connections[cloudConnectionId];
                var meta = new MessageMetaDataDictionary();
                meta.AddAction(nameof(SendGroupExceptAsync))
                    .AddGroupName(groupName)
                    .AddExcludedIds(excludedIds);
                return WriteAllProtocolRaw(connection, meta, methodName, args);
            }
            else
            {
                // TODO. log an error if specified a wrong group name
            }
            return Task.CompletedTask;
        }

        public override Task SendGroupsAsyncFromCloud(IReadOnlyList<string> groupNames, string methodName, object[] args, string cloudConnectionId)
        {
            HubConnectionContext connection = _connections[cloudConnectionId];
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendGroupsAsync))
                .AddGroupsName(groupNames);
            return WriteAllProtocolRaw(connection, meta, methodName, args);
        }

        public override Task SendUserAsyncFromCloud(string userId, string methodName, object[] args, string cloudConnectionId)
        {
            HubConnectionContext connection = _connections[cloudConnectionId];
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendUserAsync))
                .AddUserId(userId);
            return WriteAllProtocolRaw(connection, meta, methodName, args);
        }

        public override Task SendUsersAsyncFromCloud(IReadOnlyList<string> userIds, string methodName, object[] args, string cloudConnectionId)
        {
            HubConnectionContext connection = _connections[cloudConnectionId];
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendUsersAsync))
                .AddUserIds(userIds);
            return WriteAllProtocolRaw(connection, meta, methodName, args);
        }

        #region private method
        private Task WriteAllProtocolRaw(HubConnectionContext connection, IDictionary<string, string> meta, string method, object[] args)
        {
            var serviceContext = (CloudHubConnectionContext)connection;
            return serviceContext.SendAllProtocolRaw(meta, method, args);
        }

        private Task WriteRaw(HubConnectionContext connection, IDictionary<string, string> meta, string method, object[] args)
        {
            var serviceContext = (CloudHubConnectionContext)connection;
            return serviceContext.SendRaw(meta, method, args);
        }

        private void CheckNullConnectionId(string connectionId)
        {
            if (connectionId == null)
            {
                throw new ArgumentNullException(nameof(connectionId));
            }
        }

        private void CheckNullGroup(string groupName)
        {
            if (groupName == null)
            {
                throw new ArgumentNullException(nameof(groupName));
            }
        }
        #endregion

        #region never_implemented_functions
        public override Task SendAllAsync(string methodName, object[] args)
        {
            // Never invoked.
            throw new NotImplementedException();
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            // Never invoked.
            throw new NotImplementedException();
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args)
        {
            // Never invoked.
            throw new NotImplementedException();
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args)
        {
            // never invoked
            throw new NotImplementedException();
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args)
        {
            // Never invoked.
            throw new NotImplementedException();
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            // Never invoked.
            throw new NotImplementedException();
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args)
        {
            // Never invoked.
            throw new NotImplementedException();
        }
        public override Task SendUserAsync(string userId, string methodName, object[] args)
        {
            // Never invoked.
            throw new NotImplementedException();
        }
        #endregion
    }
}
