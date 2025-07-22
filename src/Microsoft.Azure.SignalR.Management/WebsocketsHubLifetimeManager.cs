// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

using Azure;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management;

internal class WebSocketsHubLifetimeManager<THub> : ServiceLifetimeManagerBase<THub>, IServiceHubLifetimeManager<THub> where THub : Hub
{
    private readonly IOptions<ServiceManagerOptions> _serviceManagerOptions;
#if NET7_0_OR_GREATER
    private static readonly TimeSpan DefaultInvocationTimeoutTimespan = TimeSpan.FromSeconds(100);
#endif
    private readonly IClientInvocationManager _clientInvocationManager;
    private readonly string _callerId;
    private readonly string _hub;

    public WebSocketsHubLifetimeManager(IServiceConnectionManager<THub> serviceConnectionManager, IHubProtocolResolver protocolResolver,
        IOptions<HubOptions> globalHubOptions, IOptions<HubOptions<THub>> hubOptions, ILoggerFactory loggerFactory, IOptions<ServiceManagerOptions> serviceManagerOptions,
        IClientInvocationManager clientInvocationManager, IServerNameProvider serverNameProvider, string hubName) :
        base(serviceConnectionManager, protocolResolver, globalHubOptions, hubOptions, loggerFactory?.CreateLogger(nameof(WebSocketsHubLifetimeManager<Hub>)))
    {
        _serviceManagerOptions = serviceManagerOptions ?? throw new ArgumentNullException(nameof(serviceManagerOptions));
        _clientInvocationManager = clientInvocationManager;
        _callerId = serverNameProvider.GetName();
        _hub = hubName;
    }

    public Task RemoveFromAllGroupsAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (IsInvalidArgument(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }

        var message = AppendMessageTracingId(new LeaveGroupWithAckMessage(connectionId, null));
        if (message.TracingId != null)
        {
            MessageLog.StartToRemoveConnectionFromGroup(Logger, message);
        }
        return WriteAckableMessageAsync(message, cancellationToken);
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
        var message = AppendMessageTracingId(new UserJoinGroupWithAckMessage(userId, groupName, 0));
        if (message.TracingId != null)
        {
            // todo: generate ack id on ctor, so that we can log ack id
            MessageLog.StartToAddUserToGroup(Logger, message);
        }
        return WriteAckableMessageAsync(message, cancellationToken);
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
        var message = AppendMessageTracingId(new UserJoinGroupWithAckMessage(userId, groupName, 0) { Ttl = (int)ttl.TotalSeconds });
        if (message.TracingId != null)
        {
            // todo: generate ack id on ctor, so that we can log ack id
            MessageLog.StartToAddUserToGroup(Logger, message);
        }
        return WriteAckableMessageAsync(message, cancellationToken);
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

        var message = AppendMessageTracingId(new UserLeaveGroupWithAckMessage(userId, groupName, 0));
        if (message.TracingId != null)
        {
            // todo: generate ack id on ctor, so that we can log ack id
            MessageLog.StartToRemoveUserFromGroup(Logger, message);
        }
        return WriteAckableMessageAsync(message, cancellationToken);
    }

    public Task UserRemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
        }

        var message = AppendMessageTracingId(new UserLeaveGroupWithAckMessage(userId, null, 0));
        if (message.TracingId != null)
        {
            // todo: generate ack id on ctor, so that we can log ack id
            MessageLog.StartToRemoveUserFromGroup(Logger, message);
        }
        return WriteAckableMessageAsync(message, cancellationToken);
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
        return WriteAckableMessageAsync(message, cancellationToken);
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
        return WriteAckableMessageAsync(message, cancellationToken);
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
        return WriteAckableMessageAsync(message, cancellationToken);
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
        return WriteAckableMessageAsync(message, cancellationToken);
    }

    public Task SendStreamItemAsync<TItem>(string connectionId, string streamId, TItem item, CancellationToken cancellationToken = default)
    {
        if (IsInvalidArgument(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }

        if (IsInvalidArgument(streamId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(streamId));
        }

        var message = AppendMessageTracingId(new MultiConnectionDataMessage(new[] { connectionId }, SerializeAllProtocols(new StreamItemMessage(streamId, item))));
        if (message.TracingId != null)
        {
            MessageLog.StartToSendMessageToConnections(Logger, message);
        }
        return WriteAsync(message);
    }

    public Task SendStreamCompletionAsync(string connectionId, string streamId, string error, CancellationToken cancellationToken = default)
    {
        if (IsInvalidArgument(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }

        if (IsInvalidArgument(streamId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(streamId));
        }

        var message = AppendMessageTracingId(new MultiConnectionDataMessage(new[] { connectionId }, SerializeAllProtocols(new CompletionMessage(streamId, error, null, false))));
        if (message.TracingId != null)
        {
            MessageLog.StartToSendMessageToConnections(Logger, message);
        }
        return WriteAsync(message);
    }

#if NET7_0_OR_GREATER
    public override async Task<T> InvokeConnectionAsync<T>(string connectionId, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        if (IsInvalidArgument(connectionId))
        {
            throw new ArgumentNullException(nameof(connectionId));
        }

        if (IsInvalidArgument(methodName))
        {
            throw new ArgumentNullException(nameof(methodName));
        }

        // cancellationToken is required to be cancellable.

        using var cts = new CancellationTokenSource(DefaultInvocationTimeoutTimespan);
        var cancellationTokenInUse = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken).Token;
           
        var invocationId = _clientInvocationManager.Caller.GenerateInvocationId(connectionId);
        var message = AppendMessageTracingId(new ClientInvocationMessage(invocationId, connectionId, _callerId, SerializeAllProtocols(methodName, args, invocationId)));
        await WriteAsync(message);
        var task = _clientInvocationManager.Caller.AddInvocation<T>(_hub, connectionId, invocationId, cancellationTokenInUse);

        // Exception handling follows https://source.dot.net/#Microsoft.AspNetCore.SignalR.Core/DefaultHubLifetimeManager.cs,349
        try
        {
            return await task;
        }
        catch
        {
            _clientInvocationManager.Caller.RemoveInvocation(invocationId);
            throw;
        }
    }

    public override Task SetConnectionResultAsync(string connectionId, CompletionMessage result)
    {
        // This method won't get trigger because we will not be sending CompletionMessage back from serverless mode.
        // this is to honor the interface
        throw new NotImplementedException();
    }
#endif

    protected override T AppendMessageTracingId<T>(T message)
    {
        if (_serviceManagerOptions.Value.EnableMessageTracing)
        {
            message.TracingId = MessageWithTracingIdHelper.Generate();
            return message;
        }

        return base.AppendMessageTracingId(message);
    }

    public AsyncPageable<GroupMember> ListConnectionsInGroup(string groupName, int? top = null, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
        }

        if (top <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(top), "Top must be greater than 0.");
        }

        ulong? tracingId = _serviceManagerOptions.Value.EnableMessageTracing ? MessageWithTracingIdHelper.Generate() : null;
        return new PagenableGroupMember((string? continuationToken, int? pageSize) => ServiceConnectionContainer.ListConnectionsInGroupAsync(groupName, top, pageSize, continuationToken, tracingId, token), token);
    }
}
