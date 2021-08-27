﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class WebSocketsHubLifetimeManager<THub> : ServiceLifetimeManagerBase<THub>, IServiceHubLifetimeManager where THub : Hub
    {
        private IOptions<ServiceManagerOptions> _serviceManagerOptions;

        public WebSocketsHubLifetimeManager(IServiceConnectionManager<THub> serviceConnectionManager, IHubProtocolResolver protocolResolver,
            IOptions<HubOptions> globalHubOptions, IOptions<HubOptions<THub>> hubOptions, ILoggerFactory loggerFactory, IOptions<ServiceManagerOptions> serviceManagerOptions) :
            base(serviceConnectionManager, protocolResolver, globalHubOptions, hubOptions, loggerFactory?.CreateLogger(nameof(WebSocketsHubLifetimeManager<Hub>)))
        {
            _serviceManagerOptions = serviceManagerOptions ?? throw new ArgumentNullException(nameof(serviceManagerOptions));
        }

        public Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            // todo: apply to other methods
            // todo: apply to transient mode
            var message = AppendMessageTracingId(new UserJoinGroupMessage(userId, groupName));
            if (message.TracingId != null)
            {
                MessageLog.StartToAddUserToGroup(Logger, message);
            }
            return WriteAsync(message);
        }

        public Task UserAddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            if (ttl < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ttl), TtlOutOfRangeErrorMessage);
            }
            var message = AppendMessageTracingId(new UserJoinGroupMessage(userId, groupName) { Ttl = (int)ttl.TotalSeconds });
            if (message.TracingId != null)
            {
                MessageLog.StartToAddUserToGroup(Logger, message);
            }
            return WriteAsync(message);
        }

        public Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var message = AppendMessageTracingId(new UserLeaveGroupMessage(userId, groupName));
            if (message.TracingId != null)
            {
                MessageLog.StartToRemoveUserFromGroup(Logger, message);
            }
            return WriteAsync(message);
        }

        public Task UserRemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            var message = AppendMessageTracingId(new UserLeaveGroupMessage(userId, null));
            if (message.TracingId != null)
            {
                MessageLog.StartToRemoveUserFromGroup(Logger, message);
            }
            return WriteAsync(message);
        }

        public Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var message = AppendMessageTracingId(new CheckUserInGroupWithAckMessage(userId, groupName));
            if (message.TracingId != null)
            {
                MessageLog.StartToCheckIfUserInGroup(Logger, message);
            }
            return WriteAckableMessageAsync(message);
        }

        public Task CloseConnectionAsync(string connectionId, string reason, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            var message = AppendMessageTracingId(new CloseConnectionMessage(connectionId, reason));
            if (message.TracingId != null)
            {
                MessageLog.StartToCloseConnection(Logger, message);
            }
            return WriteAsync(message);
        }

        public Task<bool> ConnectionExistsAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            var message = AppendMessageTracingId(new CheckConnectionExistenceWithAckMessage(connectionId));
            if (message.TracingId != null)
            {
                MessageLog.StartToCheckIfConnectionExists(Logger, message);
            }
            return WriteAckableMessageAsync(message);
        }

        public Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            var message = AppendMessageTracingId(new CheckUserExistenceWithAckMessage(userId));
            if (message.TracingId != null)
            {
                MessageLog.StartToCheckIfUserExists(Logger, message);
            }
            return WriteAckableMessageAsync(message);
        }

        public Task<bool> GroupExistsAsync(string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var message = AppendMessageTracingId(new CheckGroupExistenceWithAckMessage(groupName));
            if (message.TracingId != null)
            {
                MessageLog.StartToCheckIfGroupExists(Logger, message);
            }
            return WriteAckableMessageAsync(message);
        }

        protected override T AppendMessageTracingId<T>(T message)
        {
            if (_serviceManagerOptions.Value.EnableMessageTracing)
            {
                message.TracingId = MessageWithTracingIdHelper.Generate();
                return message;
            }

            return base.AppendMessageTracingId(message);
        }
    }
}