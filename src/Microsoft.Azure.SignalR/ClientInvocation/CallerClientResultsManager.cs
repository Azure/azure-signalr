﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET7_0_OR_GREATER
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
    internal sealed class CallerClientResultsManager : ICallerClientResultsManager, IInvocationBinder
    {
        private readonly ConcurrentDictionary<string, PendingInvocation> _pendingInvocations = new();
        private readonly string _clientResultManagerId = Guid.NewGuid().ToString("N");
        private long _lastInvocationId = 0;

        private readonly IHubProtocolResolver _hubProtocolResolver;

        public CallerClientResultsManager(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver ?? throw new ArgumentNullException(nameof(hubProtocolResolver));
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
                    typeof(T), connectionId, tcs,
                    static (state, completionMessage) =>
                    {
                        var tcs = (TaskCompletionSourceWithCancellation<T>)state;
                        if (completionMessage.HasResult)
                        {
                            tcs.TrySetResult((T)completionMessage.Result);
                        }
                        else
                        {
                            tcs.TrySetException(new Exception(completionMessage.Error));
                        }
                    }) { RouterInstanceId = instanceId }
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
                }
                else
                {
                    // do nothing
                }
            }
            else
            {
                // do nothing
            }
        }

        public void CleanupInvocations(string instanceId)
        {
            foreach (var (invocationId, invocation) in _pendingInvocations)
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
                    // Follow https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/common/Shared/ClientResultsManager.cs#L58
                    throw new InvalidOperationException($"Connection ID '{connectionId}' is not valid for invocation ID '{message.InvocationId}'.");
                }

                // if false the connection disconnected right after the above TryGetValue
                // or someone else completed the invocation (likely a bad client)
                // we'll ignore both cases
                if (_pendingInvocations.TryRemove(message.InvocationId, out _))
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
            var protocol = _hubProtocolResolver.GetProtocol(message.Protocol, null);
            if (protocol == null)
            {
                var errorMessage = $"Not supported protocol {message.Protocol} by server.";
                return TryCompleteResult(connectionId, new CompletionMessage(message.InvocationId, errorMessage, null, false));
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
                     var errorMessage = $"The payload of ClientCompletionMessage whose type is {hubMessage.GetType().Name} cannot be parsed into CompletionMessage correctly.";
                    return TryCompleteResult(connectionId, new CompletionMessage(message.InvocationId, errorMessage, null, false));
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

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => throw new NotImplementedException();

        // Unused, here to honor the IInvocationBinder interface but should never be called
        public Type GetStreamItemType(string streamId) => throw new NotImplementedException();

        private record PendingInvocation(Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)
        {
            public string RouterInstanceId { get; set; }
        }
    }
}
#endif