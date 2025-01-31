// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET7_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal sealed class WeakCallerClientResultsManager : ICallerClientResultsManager, IInvocationBinder
    {
        private readonly ConcurrentDictionary<string, PendingInvocation> _pendingInvocations = new();
        private readonly string _clientResultManagerId = Guid.NewGuid().ToString("N");
        private long _lastInvocationId;
        private readonly AckHandler _ackHandler;
        private readonly IServiceEndpointManager _endpointManager;
        private readonly IEndpointRouter _endpointRouter;
        private readonly IHubProtocolResolver _hubProtocolResolver;

        public WeakCallerClientResultsManager(IServiceEndpointManager serviceEndpointManager, IEndpointRouter endpointRouter, AckHandler ackHandler, IHubProtocolResolver hubProtocolResolver)
        {
            _endpointManager = serviceEndpointManager;
            _endpointRouter = endpointRouter;
            _ackHandler = ackHandler;
            _hubProtocolResolver = hubProtocolResolver;
        }

        public string GenerateInvocationId(string connectionId)
        {
            return $"{connectionId}-{_clientResultManagerId}-{Interlocked.Increment(ref _lastInvocationId)}";
        }

        public Task<T> AddInvocation<T>(string hub, string connectionId, string invocationId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSourceWithCancellation<T>(
                cancellationToken,
                () => TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, "Canceled")));

            var serviceEndpoints = _endpointManager.GetEndpoints(hub);
            var expectedAckCount = _endpointRouter.GetEndpointsForConnection(connectionId, serviceEndpoints).Count();

            var multiAck = _ackHandler.CreateMultiAck(out var ackId);

            _ackHandler.SetExpectedCount(ackId, expectedAckCount);

            var result = _pendingInvocations.TryAdd(invocationId,
                new PendingInvocation(
                    typeof(T), connectionId, tcs, ackId, multiAck,
                    static (state, completionMessage) =>
                    {
                        var tcs = (TaskCompletionSourceWithCancellation<T>)state;
                        if (completionMessage.HasResult)
                        {
                            tcs.TrySetResult((T)completionMessage.Result);
                        }
                        else
                        {
                            // Follow https://github.com/dotnet/aspnetcore/blob/v8.0.0-rc.2.23480.2/src/SignalR/common/Shared/ClientResultsManager.cs#L30
                            tcs.TrySetException(new HubException(completionMessage.Error));
                        }
                    })
            );

            tcs.RegisterCancellation();

            return tcs.Task;
        }

        public void CleanupInvocationsByInstance(string instanceId) { }

        public void CleanupInvocationsByConnection(string connectionId)
        {
            foreach (var (invocationId, invocation) in _pendingInvocations)
            {
                if (invocation.ConnectionId == connectionId)
                {
                    var message = CompletionMessage.WithError(invocationId, $"Connection '{invocation.ConnectionId}' is disconnected.");

                    invocation.Complete(invocation.Tcs, message);
                    _pendingInvocations.TryRemove(invocationId, out _);
                }
            }
        }

        public bool TryCompleteResult(string connectionId, CompletionMessage message)
        {
            if (_pendingInvocations.TryGetValue(message.InvocationId, out var item))
            {
                if (item.ConnectionId != connectionId)
                {
                    // Follow https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/common/Shared/ClientResultsManager.cs#L58
                    throw new InvalidOperationException($"Connection ID '{connectionId}' is not valid for invocation ID '{message.InvocationId}'.");
                }

                // Considering multiple endpoints, wait until 
                // 1. Received a non-error CompletionMessage
                // or 2. Received messages from all endpoints
                _ackHandler.TriggerAck(item.AckId);
                if (message.HasResult || item.ackTask.IsCompletedSuccessfully)
                {
                    // if false the connection disconnected right after the above TryGetValue
                    // or someone else completed the invocation (likely a bad client)
                    // we'll ignore both cases
                    if (_pendingInvocations.TryRemove(message.InvocationId, out _))
                    {
                        item.Complete(item.Tcs, message);
                        return true;
                    }
                }
                return false;
            }
            else
            {
                // connection was disconnected or someone else completed the invocation
                return false;
            }
        }

        public bool TryCompleteResult(string connectionId, ClientCompletionMessage message)
        {
            var protocol = _hubProtocolResolver.GetProtocol(message.Protocol, null);
            if (protocol == null)
            {
                var errorMessage = $"Not supported protocol {message.Protocol} by server.";
                return TryCompleteResult(connectionId, CompletionMessage.WithError(message.InvocationId, errorMessage));
            }

            var payload = message.Payload;
            if (protocol.TryParseMessage(ref payload, this, out var hubMessage))
            {
                if (hubMessage is CompletionMessage completionMessage)
                {
                    return TryCompleteResult(connectionId, completionMessage);
                }
                else
                {
                    throw new InvalidOperationException($"The payload of ClientCompletionMessage whose type is {hubMessage.GetType().Name} cannot be parsed into CompletionMessage correctly.");
                }
            }
            return false;
        }

        public bool TryCompleteResult(string connectionId, ErrorCompletionMessage message)
        {
            var errorMessage = CompletionMessage.WithError(message.InvocationId, message.Error);
            return TryCompleteResult(connectionId, errorMessage);
        }

        // Implemented for interface IInvocationBinder
        public Type GetReturnType(string invocationId)
        {
            if (TryGetInvocationReturnType(invocationId, out var type))
            {
                return type;
            }
            // This exception will be handled by https://github.com/dotnet/aspnetcore/blob/f96dce6889fe67aaed33f0c2b147b8b537358f1e/src/SignalR/common/Shared/TryGetReturnType.cs#L14 with a silent failure. The user won't be interrupted.
            throw new InvalidOperationException($"Invocation ID '{invocationId}' is not associated with a pending client result.");
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            if (_pendingInvocations.TryGetValue(invocationId, out var item))
            {
                type = item.Type;
                return true;
            }
            type = null;
            return false;
        }

        public void RemoveInvocation(string invocationId)
        {
            _pendingInvocations.TryRemove(invocationId, out _);
        }

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => throw new NotImplementedException();

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public Type GetStreamItemType(string streamId) => throw new NotImplementedException();

        public void AddServiceMapping(ServiceMappingMessage serviceMappingMessage) => throw new NotImplementedException();

        private record PendingInvocation(Type Type, string ConnectionId, object Tcs, int AckId, Task ackTask, Action<object, CompletionMessage> Complete)
        {
        }
    }
}
#endif
