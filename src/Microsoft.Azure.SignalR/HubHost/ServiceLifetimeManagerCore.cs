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

namespace Microsoft.Azure.SignalR
{
    internal class ServiceLifetimeManagerCore<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        private readonly IReadOnlyList<IHubProtocol> _allProtocols;

        private readonly IServiceConnectionContainer _serviceConnectionContainer;

        public ServiceLifetimeManagerCore(IServiceConnectionContainer serviceConnectionContainer,
            IClientConnectionManager clientConnectionManager, IHubProtocolResolver protocolResolver,
            AzureSignalRMarkerService marker)
        {
            _serviceConnectionContainer = serviceConnectionContainer;
            _allProtocols = protocolResolver.AllProtocols;
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
                return Task.CompletedTask;
            }

            return _serviceConnectionContainer.WriteAsync(
                new BroadcastDataMessage(null, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            return _serviceConnectionContainer.WriteAsync(
                new BroadcastDataMessage(excludedIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId) || IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            if (!_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var serviceConnectionContext))
            {
                // Connection isn't on this server so serialize to all protocols and send it to the service
                return _serviceConnectionContainer.WriteAsync(
                    new MultiConnectionDataMessage(new[] { connectionId }, SerializeAllProtocols(methodName, args)));
            }

            var message = new InvocationMessage(methodName, args);

            // Write directly to this connection
            return serviceConnectionContext.HubConnectionContext.WriteAsync(message).AsTask();
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionIds) || IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            return _serviceConnectionContainer.WriteAsync(
                new MultiConnectionDataMessage(connectionIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupName) || IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            var message = new GroupBroadcastDataMessage(groupName, null, SerializeAllProtocols(methodName, args));
            // Send this message from a fixed service connection, so that message order can be reserved.
            return _serviceConnectionContainer.WriteAsync(groupName, message);
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupNames))
            {
                return Task.CompletedTask;
            }

            // Send this message from a random service connection because this message involves of multiple groups.
            // Unless we send message for each group one by one, we can not guarantee the message order for all groups.
            return _serviceConnectionContainer.WriteAsync(
                new MultiGroupBroadcastDataMessage(groupNames, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupName) || IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            var message = new GroupBroadcastDataMessage(groupName, excludedIds, SerializeAllProtocols(methodName, args));
            // Send this message from a fixed service connection, so that message order can be reserved.
            return _serviceConnectionContainer.WriteAsync(groupName, message);
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(userId) || IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            return _serviceConnectionContainer.WriteAsync(
                new UserDataMessage(userId, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args,
            CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(userIds) || IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            return _serviceConnectionContainer.WriteAsync(
                new MultiUserDataMessage(userIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId) || IsInvalidArgument(groupName))
            {
                return Task.CompletedTask;
            }

            var message = new JoinGroupMessage(connectionId, groupName);
            // Send this message from a fixed service connection, so that message order can be reserved.
            return _serviceConnectionContainer.WriteAsync(groupName, message);
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId) || IsInvalidArgument(groupName))
            {
                return Task.CompletedTask;
            }

            var message = new LeaveGroupMessage(connectionId, groupName);
            // Send this message from a fixed service connection, so that message order can be reserved.
            return _serviceConnectionContainer.WriteAsync(groupName, message);
        }

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
            foreach (var hubProtocol in _allProtocols)
            {
                payloads.Add(hubProtocol.Name, hubProtocol.GetMessageBytes(message));
            }
            return payloads;
        }
    }
}
