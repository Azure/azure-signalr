// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
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
    internal class ClientResultsManager : IClientResultsManager, IInvocationBinder
    {
        private readonly ConcurrentDictionary<string, PendingInvocation> _pendingInvocations = new();
        private readonly ConcurrentDictionary<string, List<ServiceMappingMessage>> _serviceMappingMessages = new();
        private ulong _lastInvocationId = 0;

        private readonly IHubProtocolResolver _hubProtocolResolver;

        public ClientResultsManager(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver;
        }

        public string GetNewInvocationId(string connectionId, string serverGUID)
        {
            return $"{connectionId}-{serverGUID}-{Interlocked.Increment(ref _lastInvocationId)}";
        }

        public Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSourceWithCancellation<T>(this, connectionId, invocationId, cancellationToken);
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

        public bool TryRemoveInvocation(string invocationId, out PendingInvocation invocation)
        {
            return _pendingInvocations.TryRemove(invocationId, out invocation);
        }

        public void AddServiceMappingMessage(string invocationId, ServiceMappingMessage serviceMappingMessage)
        {
            if (_serviceMappingMessages.ContainsKey(serviceMappingMessage.InstanceId))
            {
                _serviceMappingMessages[serviceMappingMessage.InstanceId].Add(serviceMappingMessage);
            }
            else
            {
                _serviceMappingMessages[serviceMappingMessage.InstanceId] = new List<ServiceMappingMessage> { serviceMappingMessage };
            }
        }

        public void CleanupInvocations(string instanceId)
        {
            foreach (var serviceMappingMessage in _serviceMappingMessages[instanceId])
            {
                if (_pendingInvocations.TryRemove(serviceMappingMessage.InvocationId, out var item))
                {
                    var message = new CompletionMessage(serviceMappingMessage.InvocationId, $"Connection '{serviceMappingMessage.ConnectionId}' disconnected.", null, false);
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
                if (_pendingInvocations.Remove(message.InvocationId!, out _))
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

        public bool TryCompleteResultFromSerializedMessage(string connectionId, string protocol, ReadOnlySequence<byte> message)
        {
            var proto = _hubProtocolResolver.GetProtocol(protocol, new string[] { protocol });
            if (proto == null)
            {
                throw new InvalidOperationException($"Not supported protcol {protocol} by server");
            }

            if (proto.TryParseMessage(ref message, this, out var message1))
            {
                return TryCompleteResult(connectionId, message1 as CompletionMessage);
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

        // Custom TCS type to avoid the extra allocation that would be introduced if we managed the cancellation separately
        // Also makes it easier to keep track of the CancellationTokenRegistration for disposal
        internal sealed class TaskCompletionSourceWithCancellation<T> : TaskCompletionSource<T>
        {
            private readonly IClientResultsManager _clientResultsManager;
            private readonly string _connectionId;
            private readonly string _invocationId;
            private readonly CancellationToken _token;

            private CancellationTokenRegistration _tokenRegistration;

            public TaskCompletionSourceWithCancellation(IClientResultsManager clientResultsManager, string connectionId, string invocationId,
                CancellationToken cancellationToken)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                _clientResultsManager = clientResultsManager;
                _connectionId = connectionId;
                _invocationId = invocationId;
                _token = cancellationToken;
            }

            // Needs to be called after adding the completion to the dictionary in order to avoid synchronous completions of the token registration
            // not canceling when the dictionary hasn't been updated yet.
            public void RegisterCancellation()
            {
                if (_token.CanBeCanceled)
                {
                    _tokenRegistration = _token.UnsafeRegister(static o =>
                    {
                        var tcs = (TaskCompletionSourceWithCancellation<T>)o!;
                        tcs.SetCanceled();
                    }, this);
                }
            }
            public new void SetCanceled()
            {
                // TODO: RedisHubLifetimeManager will want to notify the other server (if there is one) about the cancellation
                // so it can clean up state and potentially forward that info to the connection
                _clientResultsManager.TryCompleteResult(_connectionId, CompletionMessage.WithError(_invocationId, "Canceled"));
            }

            public new void SetResult(T result)
            {
                _tokenRegistration.Dispose();
                base.SetResult(result);
            }

            public new void SetException(Exception exception)
            {
                _tokenRegistration.Dispose();
                base.SetException(exception);
            }

#pragma warning disable IDE0060 // Remove unused parameter
            // Just making sure we don't accidentally call one of these without knowing
            public static new void SetCanceled(CancellationToken cancellationToken) => Debug.Assert(false);

            public static new void SetException(IEnumerable<Exception> exceptions) => Debug.Assert(false);
            public static new bool TrySetCanceled()
            {
                Debug.Assert(false);
                return false;
            }
            public static new bool TrySetCanceled(CancellationToken cancellationToken)
            {
                Debug.Assert(false);
                return false;
            }
            public static new bool TrySetException(IEnumerable<Exception> exceptions)
            {
                Debug.Assert(false);
                return false;
            }
            public static new bool TrySetException(Exception exception)
            {
                Debug.Assert(false);
                return false;
            }
            public static new bool TrySetResult(T result)
            {
                Debug.Assert(false);
                return false;
            }
#pragma warning restore IDE0060 // Remove unused parameter
        }
    }
}
#else
namespace Microsoft.Azure.SignalR
{ 
    internal class ClientResultsManager: IClientResultsManager
    {

    }
}
#endif