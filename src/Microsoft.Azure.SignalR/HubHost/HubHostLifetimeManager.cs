// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class HubHostLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        private readonly ILogger<HubHostLifetimeManager<THub>> _logger;
        private readonly IReadOnlyList<IHubProtocol> _allProtocols;

        private long _nextInvocationId;
        private string InvocationId => Interlocked.Increment(ref _nextInvocationId).ToString();
        private IServiceConnectionManager _serviceConnectionManager;
        private IClientConnectionManager _clientConnectionManager;

        public HubHostLifetimeManager(IServiceConnectionManager serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IHubProtocolResolver protocolResolver, ILogger<HubHostLifetimeManager<THub>> logger)
        {
            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _allProtocols = protocolResolver.AllProtocols;
            _logger = logger;
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        public override Task SendAllAsync(string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;
            return _serviceConnectionManager.SendServiceMessage(
                new BroadcastDataMessage(null, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;
            return _serviceConnectionManager.SendServiceMessage(
                new BroadcastDataMessage(excludedIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;
            // TODO. Do not need to serialize to all protocols.
            // After update SignalR, do not forget to fix this. It impacts "echo" performance.
            return _serviceConnectionManager.SendServiceMessage(
                new MultiConnectionDataMessage(new string[1] { connectionId }, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(connectionIds), connectionIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            return _serviceConnectionManager.SendServiceMessage(
                new MultiConnectionDataMessage(connectionIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;
            return _serviceConnectionManager.SendServiceMessage(
                new GroupBroadcastDataMessage(groupName, null, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(groupNames), groupNames)) return Task.CompletedTask;

            return _serviceConnectionManager.SendServiceMessage(
                new MultiGroupBroadcastDataMessage(groupNames, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            return _serviceConnectionManager.SendServiceMessage(
                new GroupBroadcastDataMessage(groupName, excludedIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(userId), userId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            return _serviceConnectionManager.SendServiceMessage(
                new UserDataMessage(userId, SerializeAllProtocols(methodName, args)));
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(userIds), userIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            return _serviceConnectionManager.SendServiceMessage(
                new MultiUserDataMessage(userIds, SerializeAllProtocols(methodName, args)));
        }

        public override Task AddToGroupAsync(string connectionId, string groupName)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            return _serviceConnectionManager.SendServiceMessage(new JoinGroupMessage(connectionId, groupName));
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            return _serviceConnectionManager.SendServiceMessage(new LeaveGroupMessage(connectionId, groupName));
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

        private IDictionary<string, byte[]> SerializeAllProtocols(string method, object[] args)
        {
            var payloads = new Dictionary<string, byte[]>();
            var message = CreateInvocationMessage(method, args);
            foreach (var hubProtocol in _allProtocols)
            {
                payloads.Add(hubProtocol.Name, hubProtocol.GetMessageBytes(message).ToArray());
            }
            return payloads;
        }

        public InvocationMessage CreateInvocationMessage(string methodName, object[] args)
        {
            return new InvocationMessage(methodName, args);
        }
    }
}
