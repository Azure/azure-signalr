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
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceLifetimeManagerBase<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        protected const string NullOrEmptyStringErrorMessage = "Argument cannot be null or empty.";
        protected const string TtlOutOfRangeErrorMessage = "Ttl cannot be less than 0.";
        protected readonly IServiceConnectionManager<THub> ServiceConnectionContainer;
        protected ILogger Logger { get; set; }

        private readonly DefaultHubMessageSerializer _messageSerializer;

        public ServiceLifetimeManagerBase(IServiceConnectionManager<THub> serviceConnectionManager, IHubProtocolResolver protocolResolver, IOptions<HubOptions> globalHubOptions, IOptions<HubOptions<THub>> hubOptions, ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ServiceConnectionContainer = serviceConnectionManager;
            _messageSerializer = new DefaultHubMessageSerializer(protocolResolver, globalHubOptions.Value.SupportedProtocols, hubOptions.Value.SupportedProtocols);
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
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = AppendMessageTracingId(new BroadcastDataMessage(null, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToBroadcastMessage(Logger, message);
            }
            return WriteAsync(message);
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = AppendMessageTracingId(new BroadcastDataMessage(excludedIds, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToBroadcastMessage(Logger, message);
            }
            return WriteAsync(message);
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = AppendMessageTracingId(new MultiConnectionDataMessage(new[] { connectionId }, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToSendMessageToConnections(Logger, message);
            }
            return WriteAsync(message);
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionIds))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionIds));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (connectionIds.Count == 0)
            {
                return Task.CompletedTask;
            }

            var message = AppendMessageTracingId(new MultiConnectionDataMessage(connectionIds, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToSendMessageToConnections(Logger, message);
            }
            return WriteAsync(message);
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = AppendMessageTracingId(new GroupBroadcastDataMessage(groupName, null, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToBroadcastMessageToGroup(Logger, message);
            }
            return WriteAsync(message);
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupNames))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupNames));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (groupNames.Count == 0)
            {
                return Task.CompletedTask;
            }

            var message = AppendMessageTracingId(new MultiGroupBroadcastDataMessage(groupNames, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToBroadcastMessageToGroups(Logger, message);
            }
            // Send this message from a random service connection because this message involves of multiple groups.
            // Unless we send message for each group one by one, we can not guarantee the message order for all groups.
            return WriteAsync(message);
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = AppendMessageTracingId(new GroupBroadcastDataMessage(groupName, excludedIds, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToBroadcastMessageToGroup(Logger, message);
            }
            return WriteAsync(message);
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var message = AppendMessageTracingId(new UserDataMessage(userId, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToSendMessageToUser(Logger, message);
            }
            return WriteAsync(message);
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args,
            CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(userIds))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userIds));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (userIds.Count == 0)
            {
                return Task.CompletedTask;
            }

            var message = AppendMessageTracingId(new MultiUserDataMessage(userIds, SerializeAllProtocols(methodName, args)));
            if (message.TracingId != null)
            {
                MessageLog.StartToSendMessageToUsers(Logger, message);
            }
            return WriteAsync(message);
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (IsInvalidArgument(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var message = AppendMessageTracingId(new JoinGroupWithAckMessage(connectionId, groupName));
            if (message.TracingId != null)
            {
                MessageLog.StartToAddConnectionToGroup(Logger, message);
            }
            return WriteAckableMessageAsync(message);
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (IsInvalidArgument(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var message = AppendMessageTracingId(new LeaveGroupWithAckMessage(connectionId, groupName));
            if (message.TracingId != null)
            {
                MessageLog.StartToRemoveConnectionFromGroup(Logger, message);
            }
            return WriteAckableMessageAsync(message);
        }

        protected Task WriteAsync<T>(T message) where T : ServiceMessage, IMessageWithTracingId =>
            WriteCoreAsync(message, m => ServiceConnectionContainer.WriteAsync(message));

        protected Task<bool> WriteAckableMessageAsync<T>(T message) where T : ServiceMessage, IMessageWithTracingId =>
            WriteAckableCoreAsync(message, m => ServiceConnectionContainer.WriteAckableMessageAsync(m));

        protected static bool IsInvalidArgument(string value)
        {
            return string.IsNullOrEmpty(value);
        }

        protected static bool IsInvalidArgument(IReadOnlyList<object> list)
        {
            return list == null;
        }

        protected IDictionary<string, ReadOnlyMemory<byte>> SerializeAllProtocols(string method, object[] args)
        {
            var payloads = new Dictionary<string, ReadOnlyMemory<byte>>();
            var message = new InvocationMessage(method, args);
            var serializedHubMessages = _messageSerializer.SerializeMessage(message);
            foreach (var serializedMessage in serializedHubMessages)
            {
                payloads.Add(serializedMessage.ProtocolName, serializedMessage.Serialized);
            }
            return payloads;
        }

        protected ReadOnlyMemory<byte> SerializeProtocol(string protocol, string method, object[] args) =>
            _messageSerializer.SerializeMessage(protocol, new InvocationMessage(method, args));

        protected virtual T AppendMessageTracingId<T>(T message) where T : ServiceMessage, IMessageWithTracingId
        {
            return message.WithTracingId();
        }

        private async Task WriteCoreAsync<T>(T message, Func<T, Task> task) where T : ServiceMessage, IMessageWithTracingId
        {
            try
            {
                await task(message);
            }
            catch (Exception ex)
            {
                MessageLog.FailedToSendMessage(Logger, message, ex);
                throw;
            }
            if (message.TracingId != null)
            {
                MessageLog.SucceededToSendMessage(Logger, message);
            }
        }

        private async Task<bool> WriteAckableCoreAsync<T>(T message, Func<T, Task<bool>> task) where T : ServiceMessage, IMessageWithTracingId
        {
            try
            {
                var result = await task(message);
                if (message.TracingId != null)
                {
                    MessageLog.SucceededToSendMessage(Logger, message);
                }
                return result;
            }
            catch (Exception ex)
            {
                MessageLog.FailedToSendMessage(Logger, message, ex);
                throw;
            }
        }
    }
}