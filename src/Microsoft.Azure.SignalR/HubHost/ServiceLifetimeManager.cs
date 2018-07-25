// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        private const string MarkerNotConfiguredError =
            "'AddAzureSignalR(...)' was called without a matching call to 'IApplicationBuilder.UseAzureSignalR(...)'.";

        private readonly ILogger<ServiceLifetimeManager<THub>> _logger;
        private readonly IReadOnlyList<IHubProtocol> _allProtocols;

        private readonly IServiceConnectionManager<THub> _serviceConnectionManager;
        private readonly IClientConnectionManager _clientConnectionManager;

        public ServiceLifetimeManager(IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager, IHubProtocolResolver protocolResolver,
            ILogger<ServiceLifetimeManager<THub>> logger, AzureSignalRMarkerService marker)
        {
            if (!marker.IsConfigured)
            {
                throw new InvalidOperationException(MarkerNotConfiguredError);
            }

            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _allProtocols = protocolResolver.AllProtocols;
            _logger = logger;
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connection.ConnectionId, out var serviceConnectionContext))
            {
                serviceConnectionContext.HubConnectionContext = connection;
            }

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

            return _serviceConnectionManager.WriteAsync(
                new BroadcastDataMessage(null, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            return _serviceConnectionManager.WriteAsync(
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
                return _serviceConnectionManager.WriteAsync(
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

            return _serviceConnectionManager.WriteAsync(
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
            return _serviceConnectionManager.WriteAsync(message, groupName.GetHashCode());
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupNames))
            {
                return Task.CompletedTask;
            }

            // Send this message from a random service connection because this message involves of multiple groups.
            // Unless we send message for each group one by one, we can not guarantee the message order for all groups.
            return _serviceConnectionManager.WriteAsync(
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
            return _serviceConnectionManager.WriteAsync(message, groupName.GetHashCode());
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(userId) || IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            return _serviceConnectionManager.WriteAsync(
                new UserDataMessage(userId, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args,
            CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(userIds) || IsInvalidArgument(methodName))
            {
                return Task.CompletedTask;
            }

            return _serviceConnectionManager.WriteAsync(
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
            return _serviceConnectionManager.WriteAsync(message, groupName.GetHashCode());
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId) || IsInvalidArgument(groupName))
            {
                return Task.CompletedTask;
            }

            var message = new LeaveGroupMessage(connectionId, groupName);
            // Send this message from a fixed service connection, so that message order can be reserved.
            return _serviceConnectionManager.WriteAsync(message, groupName.GetHashCode());
        }

        private static bool IsInvalidArgument(string value)
        {
            return string.IsNullOrEmpty(value);
        }

        private static bool IsInvalidArgument(IReadOnlyList<object> list)
        {
            return list == null || !list.Any();
        }

        private IDictionary<string, ReadOnlyMemory<byte>> SerializeAllProtocols(string method, object[] args)
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
