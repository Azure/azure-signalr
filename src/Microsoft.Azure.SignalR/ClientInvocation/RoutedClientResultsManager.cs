// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal sealed class RoutedClientResultsManager: IRoutedClientResultsManager
    {
        private readonly ConcurrentDictionary<string, RoutedInvocation> _routedInvocations = new();
        private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _serviceMapping = new();

        public void AddInvocation(string connectionId, string invocationId, string callerServerId, string instanceId, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, "Canceled")));

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
                return _routedInvocations.TryRemove(message.InvocationId, out _);
            }
            else
            {
                // connection was disconnected or someone else completed the invocation
                return false;
            }
        }

        public bool ContainsInvocation(string invocationId)
        {
            return _routedInvocations.TryGetValue(invocationId, out _);
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            // RawResult is available when .NET >= 7.0. And client invocation also works when .NET >= 7.0
#if NET7_0_OR_GREATER
            if (_routedInvocations.TryGetValue(invocationId, out _))
            {
                type = typeof(RawResult);
                return true;
            }
#endif
            type = null;
            return false;
        }

        public void AddServiceMappingMessage(string instanceId, string invocationId)
        {
            _serviceMapping.AddOrUpdate(
                instanceId,
                new ConcurrentBag<string>() { invocationId },
                (key, valueList) => { valueList.Add(invocationId); return valueList; });
        }

        public void CleanupInvocations(string instanceId)
        {
            if (_serviceMapping.TryRemove(instanceId, out var invocationIds))
            {
                foreach (var invocationId in invocationIds)
                {
                    _routedInvocations.TryRemove(invocationId, out _);
                }
            }
        }
    }

    internal record struct RoutedInvocation(string ConnectionId, string CallerServerId)
    {
    }
}