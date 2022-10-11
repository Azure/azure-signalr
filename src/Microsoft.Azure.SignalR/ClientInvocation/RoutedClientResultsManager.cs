// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal sealed class RoutedClientResultsManager: BaseClientResultsManager, IRoutedClientResultsManager
    {
        private readonly ConcurrentDictionary<string, RoutedInvocation> _routedInvocations = new();

        public void AddInvocation(string connectionId, string invocationId, string callerServerId, string instanceId, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, "Canceled")));

            var result = _routedInvocations.TryAdd(invocationId, new RoutedInvocation(connectionId, callerServerId));
            Debug.Assert(result);

            AddServiceMappingMessage(new ServiceMappingMessage(invocationId, connectionId, instanceId));
        }

        public override bool TryCompleteResult(string connectionId, CompletionMessage message)
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

        public override void CleanupInvocations(string instanceId)
        {
            if (_serviceMapping.TryRemove(instanceId, out var invocationDict))
            {
                foreach (var invocationId in invocationDict.Keys)
                {
                    _routedInvocations.TryRemove(invocationId, out _);
                }
            }
        }

        public override bool TryGetInvocationReturnType(string invocationId, out Type type)
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
    }

    internal record struct RoutedInvocation(string ConnectionId, string CallerServerId)
    {
    }
}