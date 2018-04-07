// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private readonly HubConnectionStore _connections = new HubConnectionStore();
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

            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendAllAsync));
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendAllExceptAsync))
                .AddExcludedIds(excludedIds);

            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var connection = _connections[connectionId];
            if (IsNullObject(connection, $"Connection[{connectionId}] not found.")) return Task.CompletedTask;

            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendConnectionAsync))
                .AddConnectionId(connectionId);
            return SerializeAndSendAsync(_clientConnectionManager.ClientTransferFormat(connectionId), meta, methodName, args);
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(connectionIds), connectionIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendConnectionsAsync))
                .AddConnectionIds(connectionIds);
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
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

            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendGroupAsync))
                .AddGroupName(groupName);
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(groupNames), groupNames)) return Task.CompletedTask;
            // Shall we validate group? The same issue as above.
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendGroupsAsync))
                .AddGroupsName(groupNames);
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds)
        {
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendGroupExceptAsync))
                .AddGroupName(groupName)
                .AddExcludedIds(excludedIds);
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args)
        {
            if (IsInvalidStringArgument(nameof(userId), userId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendUserAsync))
                .AddUserId(userId);
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args)
        {
            if (IsInvalidListArgument(nameof(userIds), userIds)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(methodName), methodName)) return Task.CompletedTask;

            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(SendUsersAsync))
                .AddUserIds(userIds);
            return SerializeAllProtocolsAndSendAsync(meta, methodName, args);
        }

        public override Task AddGroupAsync(string connectionId, string groupName)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            var connection = _connections[connectionId];
            if (IsNullObject(connection, $"Connection[{connectionId}] not found.")) return Task.CompletedTask;

            //_groups.Add(connection, groupName);
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(AddGroupAsync))
                .AddConnectionId(connectionId)
                .AddGroupName(groupName);
            return SerializeAndSendAsync(_clientConnectionManager.ClientTransferFormat(connectionId), meta, null, null);
        }

        public override Task RemoveGroupAsync(string connectionId, string groupName)
        {
            if (IsInvalidStringArgument(nameof(connectionId), connectionId)) return Task.CompletedTask;
            if (IsInvalidStringArgument(nameof(groupName), groupName)) return Task.CompletedTask;

            var connection = _connections[connectionId];
            if (IsNullObject(connection, $"Connection[{connectionId}] not found.")) return Task.CompletedTask;

            //_groups.Remove(connectionId, groupName);
            // Ask SignalR Service to do 'RemoveGroupAsync'
            var meta = new MessageMetaDataDictionary();
            meta.AddAction(nameof(RemoveGroupAsync))
                .AddConnectionId(connectionId)
                .AddGroupName(groupName);
            return SerializeAndSendAsync(_clientConnectionManager.ClientTransferFormat(connectionId), meta, null, null);
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

        private ServiceMessage Serialize(TransferFormat format, IDictionary<string, string> meta, string method, object[] args)
        {
            var serviceMessage = new ServiceMessage();
            serviceMessage.AddMetadata(meta);
            if (method != null)
            {
                var message = CreateInvocationMessage(method, args);
                serviceMessage.WritePayload(format,
                    format == TransferFormat.Binary ?
                    _messagePackProtocol.WriteToArray(message) : _jsonProtocol.WriteToArray(message));
            }
            return serviceMessage;
        }

        private ServiceMessage SerializeAllProtocols(IDictionary<string, string> meta, string method, object[] args)
        {
            var serviceMessage = new ServiceMessage();
            serviceMessage.AddMetadata(meta);
            if (method != null)
            {
                var message = CreateInvocationMessage(method, args);
                serviceMessage.JsonPayload = _jsonProtocol.WriteToArray(message);
                serviceMessage.MsgpackPayload = _messagePackProtocol.WriteToArray(message);
            }
            return serviceMessage;
        }

        private Task SerializeAndSendAsync(TransferFormat format, IDictionary<string, string> meta, string method, object[] args)
        {
            var serviceMessage = Serialize(format, meta, method, args);
            return _serviceConnectionManager.SendServiceMessage(serviceMessage);
        }

        private Task SerializeAllProtocolsAndSendAsync(IDictionary<string, string> meta, string method, object[] args)
        {
            var serviceMessage = SerializeAllProtocols(meta, method, args);
            return _serviceConnectionManager.SendServiceMessage(serviceMessage);
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
