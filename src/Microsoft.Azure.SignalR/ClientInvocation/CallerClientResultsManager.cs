// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR;
using System.Linq;

namespace Microsoft.Azure.SignalR
{
    internal sealed class CallerClientResultsManager : ICallerClientResultsManager, IInvocationBinder
    {
        private readonly ConcurrentDictionary<string, PendingInvocation> _pendingInvocations = new();
        private readonly string _clientResultManagerId = Guid.NewGuid().ToString();
        private long _lastInvocationId = 0;

        private readonly IHubProtocolResolver _hubProtocolResolver;

        public CallerClientResultsManager(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver;
        }

        public string GenerateInvocationId(string connectionId)
        {
            return $"{connectionId}-{_clientResultManagerId}-{Interlocked.Increment(ref _lastInvocationId)}";
        }

        public Task<T> AddInvocation<T>(string connectionId, string invocationId, string instanceId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSourceWithCancellation<T>(
                cancellationToken,
                () => TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, "Canceled")));

            // When the caller server is also the client router, Azure SignalR service won't send a ServiceMappingMessage to server.
            // To handle this condition, CallerClientResultsManager itself should record this mapping information rather than waiting for a ServiceMappingMessage sent by service. Only in this condition, this method is called with instanceId != null.
            var result = _pendingInvocations.TryAdd(invocationId, 
                new PendingInvocation(
                    typeof(T), connectionId, instanceId, tcs, 
                    static (state, completionMessage) =>
                    {
                        var tcs = (TaskCompletionSourceWithCancellation<T>)state;
                        if (completionMessage.HasResult)
                        {
                            tcs.SetResult((T)completionMessage.Result);
                        }
                        else
                        {
                            tcs.SetException(new Exception(completionMessage.Error));
                        }
                    })
            );
            Debug.Assert(result);

            tcs.RegisterCancellation();

            return tcs.Task;
        }

        public void AddServiceMapping(ServiceMappingMessage serviceMappingMessage)
        {
            if (_pendingInvocations.TryGetValue(serviceMappingMessage.InvocationId, out var invocation))
            {
                if (invocation.RouterInstanceId == null)
                {
                    invocation.RouterInstanceId = serviceMappingMessage.InstanceId;
                    _pendingInvocations[serviceMappingMessage.InvocationId] = invocation;
                }
                else
                {
                    throw new InvalidOperationException($"Failed to record a service mapping whose RouterInstanceId '{serviceMappingMessage.InvocationId}' was already existing.");
                }
            }
            else
            {
                throw new InvalidOperationException($"Failed to record a service mapping whose InvocationId '{serviceMappingMessage.InvocationId}' doesn't exist.");
            }
        }

        public void RemoveServiceMapping(string invocationId)
        {
            if (_pendingInvocations.TryGetValue(invocationId, out var invocation))
            {
                invocation.RouterInstanceId = null;
                _pendingInvocations[invocationId] = invocation;
            }
            else
            {
                // it's acceptable that the mapping information of invocationId doesn't exsits.";
            }
        }

        public void CleanupInvocations(string instanceId)
        {
            foreach (var (invocationId, invocation) in _pendingInvocations.Select(x => (x.Key, x.Value)))
            {
                if (invocation.RouterInstanceId == instanceId)
                {
                    var message = new CompletionMessage(invocationId, $"Connection '{invocation.ConnectionId}' is disconnected.", null, false);
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
                    throw new InvalidOperationException($"Connection ID '{connectionId}' is not valid for invocation ID '{message.InvocationId}'.");
                }

                // if false the connection disconnected right after the above TryGetValue
                // or someone else completed the invocation (likely a bad client)
                // we'll ignore both cases
                if (_pendingInvocations.TryRemove(message.InvocationId, out _))
                {
                    item.Complete(item.Tcs, message);
                    RemoveServiceMapping(message.InvocationId);
                    return true;
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
                throw new InvalidOperationException($"Not supported protocol {message.Protocol} by server");
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
                     throw new InvalidOperationException($"The payload of ClientCompletionMessage whose type is {hubMessage.GetType()} cannot be parsed into CompletionMessage correctly.");
                }
            }
            return false;
        }

        // Implemented for interface IInvocationBinder
        public Type GetReturnType(string invocationId)
        {
            if (TryGetInvocationReturnType(invocationId, out var type))
            {
                return type;
            }
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

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => throw new NotImplementedException();

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public Type GetStreamItemType(string streamId) => throw new NotImplementedException();
    }

    internal record struct PendingInvocation(Type Type, string ConnectionId, string RouterInstanceId, object Tcs, Action<object, CompletionMessage> Complete)
    {
    }
}
