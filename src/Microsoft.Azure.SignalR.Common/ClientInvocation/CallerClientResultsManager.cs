// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class CallerClientResultsManager : ICallerClientResultsManager, IInvocationBinder
    {
        private readonly ConcurrentDictionary<string, PendingInvocation> _pendingInvocations = new();
        private readonly ConcurrentDictionary<string, List<string>> _serviceMappingMessages = new();
        private readonly string _serverGUID = Guid.NewGuid().ToString();
        private long _lastInvocationId = 0;

        private readonly IHubProtocolResolver _hubProtocolResolver;

        public CallerClientResultsManager(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver;
        }

        public string GenerateInvocationId(string connectionId)
        {
            return $"{connectionId}-{_serverGUID}-{Interlocked.Increment(ref _lastInvocationId)}";
        }

        public Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken)
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

            return tcs.Task;
        }

        public void AddServiceMappingMessage(ServiceMappingMessage serviceMappingMessage)
        {
            _serviceMappingMessages.TryGetValue(serviceMappingMessage.InstanceId, out var oldValue);
            var newValue = oldValue ?? new List<string> { };
            newValue.Add(serviceMappingMessage.InvocationId);
            _serviceMappingMessages.TryUpdate(serviceMappingMessage.InstanceId, newValue, oldValue);
        }

        public void CleanupInvocations(string instanceId)
        {
            foreach (var invocationId in _serviceMappingMessages[instanceId])
            {
                if (_pendingInvocations.TryRemove(invocationId, out var item))
                {
                    var message = new CompletionMessage(invocationId, $"Connection '{item.ConnectionId}' disconnected.", null, false);
                    item.Complete(item.Tcs, message);
                }
            }
        }

        public bool TryCompleteResult(string connectionId, CompletionMessage message)
        {
            if (_pendingInvocations.TryGetValue(message.InvocationId!, out var item))
            {
                if (item.ConnectionId != connectionId)
                {
                    throw new InvalidOperationException($"Connection ID '{connectionId}' is not valid for invocation ID '{message.InvocationId}'.");
                }

                // if false the connection disconnected right after the above TryGetValue
                // or someone else completed the invocation (likely a bad client)
                // we'll ignore both cases
                if (_pendingInvocations.TryRemove(message.InvocationId!, out _))
                {
                    item.Complete(item.Tcs, message);
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
            var proto = _hubProtocolResolver.GetProtocol(message.Protocol, new string[] { message.Protocol });
            if (proto == null)
            {
                throw new InvalidOperationException($"Not supported protcol {message.Protocol} by server");
            }

            var payload = message.Payload;
            if (proto.TryParseMessage(ref payload, this, out var completionMessage))
            {
                return TryCompleteResult(connectionId, completionMessage as CompletionMessage);
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
        public IReadOnlyList<Type> GetParameterTypes(string methodName)
        {
            throw new NotImplementedException();
        }

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public Type GetStreamItemType(string streamId)
        {
            throw new NotImplementedException();
        }
    }

    internal record PendingInvocation(Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)
    {
    }
}
