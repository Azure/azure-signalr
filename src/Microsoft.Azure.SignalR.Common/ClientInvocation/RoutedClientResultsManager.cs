// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class RoutedClientResultsManager: IRoutedClientResultsManager
    {
        private readonly ConcurrentDictionary<string,  RoutedInvocation> _routedInvocations = new();

        public Task<object> AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSourceWithCancellation<object>(
                cancellationToken,
                () => TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, "Canceled")));
            
            var result = _routedInvocations.TryAdd(invocationId, new RoutedInvocation(connectionId, callerServerId, tcs, static (state, completionMessage) =>
            {
                var tcs = (TaskCompletionSource<object>)state;
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

        public bool TryCompleteResult(string connectionId, CompletionMessage message)
        {
            if (_routedInvocations.TryGetValue(message.InvocationId, out var item))
            {
                if (item.ConnectionId != connectionId)
                {
                    throw new InvalidOperationException($"Connection ID '{connectionId}' is not valid for invocation ID '{message.InvocationId}'.");
                }
                if (_routedInvocations.TryRemove(message.InvocationId!, out _))
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

        public bool TryGetRoutedInvocation(string invocationId, out RoutedInvocation routedInvocation)
        {
            return _routedInvocations.TryGetValue(invocationId, out routedInvocation);
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            if (_routedInvocations.TryGetValue(invocationId, out var item))
            {
                type = typeof(object);
                return true;
            }
            type = null;
            return false;
        }

        public void CleanupInvocations(string instanceId)
        {
            throw new NotImplementedException();
        }
    }
}