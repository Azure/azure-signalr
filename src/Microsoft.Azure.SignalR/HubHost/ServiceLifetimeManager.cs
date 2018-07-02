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
        private readonly ILogger<ServiceLifetimeManager<THub>> _logger;
        private readonly IReadOnlyList<IHubProtocol> _allProtocols;

        private readonly IServiceConnectionManager<THub> _serviceConnectionManager;
        private readonly IClientConnectionManager _clientConnectionManager;

        public ServiceLifetimeManager(IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IHubProtocolResolver protocolResolver, ILogger<ServiceLifetimeManager<THub>> logger,
            AzureSignalRMarkerService azureSignalRMarkerService)
        {
            if (!azureSignalRMarkerService.UseAzureSignalRFlag)
            {
                throw new InvalidOperationException("Please call UseAzureSignalR(...) when using AddAzureSignalR(...)");
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
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;
            return _serviceConnectionManager.WriteAsync(
                new BroadcastDataMessage(null, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;
            return _serviceConnectionManager.WriteAsync(
                new BroadcastDataMessage(excludedIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            if (!_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var serviceConnectionContext))
            {
                // Connection isn't on this server so serialize to all protocols and send it to the service
                return _serviceConnectionManager.WriteAsync(
                    new MultiConnectionDataMessage(new[] { connectionId }, SerializeAllProtocols(methodName, args)));
            }

            var message = CreateInvocationMessage(methodName, args);

            // Write directly to this connection
            return serviceConnectionContext.HubConnectionContext.WriteAsync(message).AsTask();
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidListArgument(nameof(connectionIds), connectionIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            return _serviceConnectionManager.WriteAsync(
                new MultiConnectionDataMessage(connectionIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;
            return _serviceConnectionManager.WriteAsync(
                new GroupBroadcastDataMessage(groupName, null, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidListArgument(nameof(groupNames), groupNames)) return Task.CompletedTask;

            return _serviceConnectionManager.WriteAsync(
                new MultiGroupBroadcastDataMessage(groupNames, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            return _serviceConnectionManager.WriteAsync(
                new GroupBroadcastDataMessage(groupName, excludedIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidStringArgument(nameof(userId), userId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            return _serviceConnectionManager.WriteAsync(
                new UserDataMessage(userId, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidListArgument(nameof(userIds), userIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            return _serviceConnectionManager.WriteAsync(
                new MultiUserDataMessage(userIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            return _serviceConnectionManager.WriteAsync(new JoinGroupMessage(connectionId, groupName));
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            return _serviceConnectionManager.WriteAsync(new LeaveGroupMessage(connectionId, groupName));
        }

        private bool IsInvalidStringArgument(string name, string value)
        {
            return IsEmptyString(value, name);
        }

        private bool IsInvalidListArgument(string name, IReadOnlyList<object> list)
        {
            if (list != null && list.Any()) return false;
            return true;
        }

        private bool IsEmptyString(string value, string name)
        {
            if (!string.IsNullOrEmpty(value)) return false;
            return true;
        }

        private IDictionary<string, ReadOnlyMemory<byte>> SerializeAllProtocols(string method, object[] args)
        {
            var payloads = new Dictionary<string, ReadOnlyMemory<byte>>();
            var message = CreateInvocationMessage(method, args);
            foreach (var hubProtocol in _allProtocols)
            {
                payloads.Add(hubProtocol.Name, hubProtocol.GetMessageBytes(message));
            }
            return payloads;
        }

        public InvocationMessage CreateInvocationMessage(string methodName, object[] args)
        {
            return new InvocationMessage(methodName, args);
        }
    }
}
