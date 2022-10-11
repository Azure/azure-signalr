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

namespace Microsoft.Azure.SignalR
{
    internal sealed class CallerClientResultsManager : BaseClientResultsManager, ICallerClientResultsManager, IInvocationBinder
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

            var result = _pendingInvocations.TryAdd(invocationId, new PendingInvocation(typeof(T), connectionId, tcs, static (state, completionMessage) =>
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
            }
            ));
            Debug.Assert(result);

            tcs.RegisterCancellation();

            // When the caller server is also the client router, Azure SignalR service won't send the ServiceMappingMessage to server.
            // To handle this condition, CallerClientResultsManager should record this mapping information itself without receiving a ServiceMappingMessage from service.
            if (instanceId != null)
            {
                AddServiceMappingMessage(new ServiceMappingMessage(connectionId, invocationId, instanceId));
            }

            return tcs.Task;
        }

        public override void CleanupInvocations(string instanceId)
        {
            if (_serviceMapping.TryRemove(instanceId, out var invocationIdDict))
            {
                foreach (var invocationId in invocationIdDict.Keys)
                {
                    if (_pendingInvocations.TryRemove(invocationId, out var item))
                    {
                        var message = new CompletionMessage(invocationId, $"Connection '{item.ConnectionId}' is disconnected.", null, false);
                        item.Complete(item.Tcs, message);
                    }
                }
            }
        }

        public override bool TryCompleteResult(string connectionId, CompletionMessage message)
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
                    RemoveServiceMappingMessage(message.InvocationId);
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

        public override bool TryGetInvocationReturnType(string invocationId, out Type type)
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

    internal record struct PendingInvocation(Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)
    {
    }
}
