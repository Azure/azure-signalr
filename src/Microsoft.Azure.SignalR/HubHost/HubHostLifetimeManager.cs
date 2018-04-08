// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
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
        // Protocol Reader/Writer shared between all HubConnectionContexts
        private readonly IHubProtocol _jsonProtocol;
        private readonly IHubProtocol _messagePackProtocol;

        private long _nextInvocationId;
        private string InvocationId => Interlocked.Increment(ref _nextInvocationId).ToString();
        private IServiceConnectionManager _serviceConnectionManager;
        private IClientConnectionManager _clientConnectionManager;

        public HubHostLifetimeManager(IServiceConnectionManager serviceConnectionManager, IClientConnectionManager clientConnectionManager, IHubProtocolResolver protocolResolver, ILogger<HubHostLifetimeManager<THub>> logger)
        {
            _serviceConnectionManager = serviceConnectionManager;
            _clientConnectionManager = clientConnectionManager;
            _jsonProtocol = protocolResolver.GetProtocol(JsonHubProtocol.ProtocolName, null);
            _messagePackProtocol = protocolResolver.GetProtocol(MessagePackHubProtocol.ProtocolName, null);
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

            // Do we need to validate the sender has joined the Group?
            // Suppose that is the developer's responsibility. So, I removed the group validation.
            //
            // Consider there are 2 SDK servers and 10 clients.
            // If 5 of clients join the same group, for example, groupA, and the 5 clients are routed to the same SDK server,
            // any of other client will see error if they want to send message to groupA.
            // But if 2 of clients are routed to SDK server1, the other 3 clients are routed to SDK server2,
            // then the other 5 clients can send to groupA even though they did not join groupA.

            var serviceMessage = new ServiceMessage().AddSendGroup(groupName);
            return SerializeAllProtocolsAndSendAsync(serviceMessage, methodName, args);
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(groupNames), groupNames)) return Task.CompletedTask;
            // Shall we validate group? The same issue as above.
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
                if (string.Equals(protocol, MessagePackHubProtocol.ProtocolName, StringComparison.Ordinal))
                {
                    serviceMessage.AddPayload(protocol, _messagePackProtocol.WriteToArray(message));
                }
                else
                {
                    serviceMessage.AddPayload(protocol, _jsonProtocol.WriteToArray(message));
                }
            }
            return serviceMessage;
        }

        private ServiceMessage SerializeAllProtocols(ServiceMessage serviceMessage, string method, object[] args)
        {
            if (method != null)
            {
                var message = CreateInvocationMessage(method, args);
                serviceMessage.AddPayload(_jsonProtocol.Name, _jsonProtocol.WriteToArray(message));
                serviceMessage.AddPayload(_messagePackProtocol.Name, _messagePackProtocol.WriteToArray(message));
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
