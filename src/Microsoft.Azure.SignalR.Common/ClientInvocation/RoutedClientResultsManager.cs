// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Protocol;
using System.Collections.Generic;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class RoutedClientResultsManager: IRoutedClientResultsManager
    {
        private readonly ConcurrentDictionary<string,  RoutedInvocation> _routedInvocations = new();
        private readonly ConcurrentDictionary<string, List<string>> _serviceMappingMessages = new();

        public void AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, string instanceId, CancellationToken cancellationToken)
        {
            var cts = new CancellationTokenSource();

            cancellationToken.Register(() => cts.Cancel());
            cts.Token.Register(() => TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, "Canceled")));

            var result = _routedInvocations.TryAdd(invocationId, new RoutedInvocation(connectionId, callerServerId));
            Debug.Assert(result);

            AddServiceMappingMessage(instanceId, invocationId);
        }

        public bool TryCompleteResult(string connectionId, CompletionMessage message)
        {
            if (_routedInvocations.TryGetValue(message.InvocationId, out var item))
            {
                if (item.ConnectionId != connectionId)
                {
                    throw new InvalidOperationException($"Connection ID '{connectionId}' is not valid for invocation ID '{message.InvocationId}'.");
                }
                return _routedInvocations.TryRemove(message.InvocationId!, out _);
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
            if (_routedInvocations.TryGetValue(invocationId, out _))
            {
                type = typeof(object);
                return true;
            }
            type = null;
            return false;
        }

        public void AddServiceMappingMessage(string instanceId, string invocationId)
        {
            _serviceMappingMessages.TryGetValue(instanceId, out var oldValue);
            var newValue = oldValue ?? new List<string> { };
            newValue.Add(invocationId);
            _serviceMappingMessages.TryUpdate(instanceId, newValue, oldValue);
        }

        public void CleanupInvocations(string instanceId)
        {
            foreach (var invocationId in _serviceMappingMessages[instanceId])
            {
                if (_routedInvocations.TryRemove(invocationId, out var item))
                {
                    var message = new CompletionMessage(invocationId, $"Connection '{item.ConnectionId}' disconnected.", null, false);
                }
            }
        }
    }

    internal record RoutedInvocation(string ConnectionId, string CallerServerId)
    {
    }
}