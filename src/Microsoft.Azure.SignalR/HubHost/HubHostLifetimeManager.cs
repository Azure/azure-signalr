// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    public class HubHostLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        //private readonly HubGroupList _groups = new HubGroupList();
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

            var meta = new ServiceMessage();
            meta.Command = CommandType.SendToAll;
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var meta = new ServiceMessage().AddExcludedIds(excludedIds);
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var serviceMessage = new ServiceMessage().AddSendConnection(connectionId);
            return SerializeAndSendAsync(_clientConnectionManager.ClientProtocol(connectionId), serviceMessage, methodName, args);
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(connectionIds), connectionIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var serviceMessage = new ServiceMessage().AddSendConnections(connectionIds);
            return SerializeAllProtocolsAndSendAsync(serviceMessage, methodName, args);
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var serviceMessage = new ServiceMessage().AddSendGroup(groupName);
            return SerializeAllProtocolsAndSendAsync(serviceMessage, methodName, args);
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(groupNames), groupNames)) return Task.CompletedTask;

            var serviceMessage = new ServiceMessage().AddSendGroups(groupNames);
            return SerializeAllProtocolsAndSendAsync(serviceMessage, methodName, args);
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var serviceMessage = new ServiceMessage().AddSendGroupExcludedIds(groupName, excludedIds);
            return SerializeAllProtocolsAndSendAsync(serviceMessage, methodName, args);
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(userId), userId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var serviceMessage = new ServiceMessage().AddSendUserId(userId);
            return SerializeAllProtocolsAndSendAsync(serviceMessage, methodName, args);
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(userIds), userIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var serviceMessage = new ServiceMessage().AddSendUserIds(userIds);
            return SerializeAllProtocolsAndSendAsync(serviceMessage, methodName, args);
        }

        public override Task AddGroupAsync(string connectionId, string groupName)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            var serviceMessage = new ServiceMessage().CreateAddConnectionToGroup(connectionId, groupName);
            return SerializeAndSendAsync(_clientConnectionManager.ClientProtocol(connectionId), serviceMessage, null, null);
        }

        public override Task RemoveGroupAsync(string connectionId, string groupName)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            var serviceMesssage = new ServiceMessage();
            serviceMesssage.CreateRemoveConnectionFromGroup(connectionId, groupName);
            return SerializeAndSendAsync(_clientConnectionManager.ClientProtocol(connectionId), serviceMesssage, null, null);
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

        private ServiceMessage Serialize(string protocol, ServiceMessage serviceMessage, string method, object[] args)
        {
            if (method != null)
            {
                var message = CreateInvocationMessage(method, args);
                foreach (var hubProtocol in _allProtocols)
                {
                    if (string.Equals(hubProtocol.Name, protocol, StringComparison.Ordinal))
                    {
                        serviceMessage.AddPayload(protocol, hubProtocol.WriteToArray(message));
                        break;
                    }
                }
            }
            return serviceMessage;
        }

        private ServiceMessage SerializeAllProtocols(ServiceMessage serviceMessage, string method, object[] args)
        {
            if (method != null)
            {
                var message = CreateInvocationMessage(method, args);
                foreach (var hubProtocol in _allProtocols)
                {
                    serviceMessage.AddPayload(hubProtocol.Name, hubProtocol.WriteToArray(message));
                }
            }
            return serviceMessage;
        }

        private Task SerializeAndSendAsync(string protocol, ServiceMessage serviceMessage, string method, object[] args)
        {
            return _serviceConnectionManager.SendServiceMessage(Serialize(protocol, serviceMessage, method, args));
        }

        private Task SerializeAllProtocolsAndSendAsync(ServiceMessage serviceMessage, string method, object[] args)
        {
            return _serviceConnectionManager.SendServiceMessage(SerializeAllProtocols(serviceMessage, method, args));
        }

        public InvocationMessage CreateInvocationMessage(string methodName, object[] args)
        {
            var invocationMessage = new InvocationMessage(
                target: methodName,
                argumentBindingException: null, arguments: args);
            return invocationMessage;
        }
    }
}
