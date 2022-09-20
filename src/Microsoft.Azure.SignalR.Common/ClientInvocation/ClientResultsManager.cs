﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Buffers;
using System;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class ClientResultsManager : IClientResultsManager, IInvocationBinder
    {
        private readonly ConcurrentDictionary<string, (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)> _pendingInvocations = new();
        private ulong _lastInvocationId = new();

        private readonly ConcurrentDictionary<string, (Type Type, string ConnectionId, string CallerServerId, object Tcs, Action<object, CompletionMessage> Complete)> _routedInvocations = new();

        private readonly ConcurrentDictionary<string, ServiceMappingMessage> _serviceMappingMessages = new();
        private readonly IHubProtocolResolver _hubProtocolResolver;

        public ClientResultsManager(IHubProtocolResolver hubProtocolResolver)
        {
            _hubProtocolResolver = hubProtocolResolver;
        }

        public ulong GetNewInvocation()
        {
            return Interlocked.Increment(ref _lastInvocationId);
        }

        public void AddServiceMappingMessage(string invocationId, ServiceMappingMessage serviceMappingMessage)
        {
            _serviceMappingMessages.TryAdd(invocationId, serviceMappingMessage);
        }

        public void RemoveServiceMappingMessageWithOfflinePing(string instanceId)
        {
            foreach (var (_, serviceMappingMessage) in _serviceMappingMessages)
            {
                if (serviceMappingMessage.InstanceId == instanceId)
                {
                    if (_pendingInvocations.TryRemove(serviceMappingMessage.InvocationId, out var item))
                    {
                        var message = new CompletionMessage(serviceMappingMessage.InvocationId, $"Connection '{serviceMappingMessage.ConnectionId}' disconnected.", null, true);
                        item.Complete(item.Tcs, message);
                    }
                }
            }
        }

        public Task<object> AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSourceWithCancellation<object>(this, connectionId, invocationId, cancellationToken);
            var result = _routedInvocations.TryAdd(invocationId, (typeof(object), connectionId, callerServerId, tcs, static (state, completionMessage) =>
            {
                var tcs = (TaskCompletionSourceWithCancellation<object>)state;
                if (completionMessage.HasResult)
                {
                    tcs.SetResult(completionMessage.Result);
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

        public Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSourceWithCancellation<T>(this, connectionId, invocationId, cancellationToken);
            var result = _pendingInvocations.TryAdd(invocationId, (typeof(T), connectionId, tcs, static (state, completionMessage) =>
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

        public void TryCompleteResult(string connectionId, string protocol, ReadOnlySequence<byte> message)
        {
            var proto = _hubProtocolResolver.GetProtocol(protocol, new string[] { protocol });
            if (proto == null)
            {
                throw new InvalidOperationException($"Not supported protcol {protocol} by server");
            }

            if (proto.TryParseMessage(ref message, this, out var message1))
            {
                TryCompleteResult(connectionId, message1 as CompletionMessage);
            }
        }

        public void TryCompleteResult(string connectionId, CompletionMessage message)
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
                }
            }
            else
            {
                // connection was disconnected or someone else completed the invocation
            }
        }

        public bool CheckRoutedInvocation(string invocationId)
        {
            return _routedInvocations.TryGetValue(invocationId, out var _);
        }

        public void TryCompleteRoutedResult(string connectionId, CompletionMessage message)
        {
            if (_routedInvocations.TryGetValue(message.InvocationId!, out var item))
            {
                if (item.ConnectionId != connectionId)
                {
                    throw new InvalidOperationException($"Connection ID '{connectionId}' is not valid for invocation ID '{message.InvocationId}'.");
                }

                // if false the connection disconnected right after the above TryGetValue
                // or someone else completed the invocation (likely a bad client)
                // we'll ignore both cases
                if (_routedInvocations.Remove(message.InvocationId!, out _))
                {
                    item.Complete(item.Tcs, message);
                }
            }
            else
            {
                // connection was disconnected or someone else completed the invocation
            }
        }

        public (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Completion)? RemoveInvocation(string invocationId)
        {
            _pendingInvocations.TryRemove(invocationId, out var item);
            return item;
        }

        public (Type Type, string ConnectionId, string callerServerId, object Tcs, Action<object, CompletionMessage> Completion)? RemoveRoutedInvocation(string invocationId)
        {
            _routedInvocations.TryRemove(invocationId, out var item);
            return item;
        }

        public Type GetReturnType(string invocationId)
        {
            if (TryGetType(invocationId, out var type))
            {
                return type;
            }
            throw new InvalidOperationException($"Invocation ID '{invocationId}' is not associated with a pending client result.");
        }

        public bool TryGetType(string invocationId, out Type type)
        {
            if (_pendingInvocations.TryGetValue(invocationId, out var item1))
            {
                type = item1.Type;
                return true;
            }
            if (_routedInvocations.TryGetValue(invocationId, out var item2))
            {
                type = item2.Type;
                return true;
            }
            type = null;
            return false;
        }

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