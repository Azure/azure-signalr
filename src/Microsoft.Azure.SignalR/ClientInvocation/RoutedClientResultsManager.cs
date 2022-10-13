// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET7_0_OR_GREATER
using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using System.Linq;

namespace Microsoft.Azure.SignalR
{
    internal sealed class RoutedClientResultsManager: IRoutedClientResultsManager
    {
        private readonly ConcurrentDictionary<string, RoutedInvocation> _routedInvocations = new();

        public void AddInvocation(string connectionId, string invocationId, string callerServerId, string instanceId, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, "Canceled")));

            var result = _routedInvocations.TryAdd(invocationId, new RoutedInvocation(connectionId, callerServerId, null));
            Debug.Assert(result);

            AddServiceMapping(new ServiceMappingMessage(invocationId, connectionId, instanceId));
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

        public void AddServiceMapping(ServiceMappingMessage serviceMappingMessage)
        {
            if (_routedInvocations.TryGetValue(serviceMappingMessage.InvocationId, out var invocation))
            {
                if (invocation.RouterInstanceId == null)
                {
                    _routedInvocations[serviceMappingMessage.InvocationId].RouterInstanceId = serviceMappingMessage.InstanceId;
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

        public void RemoveServiceMapping(string invocationId)
        {
            if (_routedInvocations.TryGetValue(invocationId, out var invocation))
            {
                _routedInvocations[invocationId].RouterInstanceId = null;
            }
            else
            {
                // it's acceptable that the mapping information of invocationId doesn't exsits.
            }
        }

        public void CleanupInvocations(string instanceId)
        {
            foreach (var (invocationId, invocation) in _routedInvocations)
            {
                if (invocation.RouterInstanceId == instanceId)
                {
                    _routedInvocations.TryRemove(invocationId, out _);
                }
            }
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            // RawResult is available when .NET >= 7.0
            if (_routedInvocations.TryGetValue(invocationId, out _))
            {
                type = typeof(RawResult);
                return true;
            }
            type = null;
            return false;
        }
    }

    class RoutedInvocation
    {
        public RoutedInvocation(string connectionId, string callerServerId, string routerInstanceId)
        {
            this.ConnectionId = connectionId;
            this.CallerServerId = callerServerId;
            this.RouterInstanceId = routerInstanceId;
        }

        public string ConnectionId { get; set; }
        
        public string CallerServerId { get; set; }
        
        public string RouterInstanceId { get; set; }
    }
}
#endif