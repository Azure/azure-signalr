// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal abstract class BaseClientResultsManager : IBaseClientResultsManager
    {
        // The first dict： InstanceId -> Invocations which are routed by the instance
        // The second dict: InvocationId -> True, representing an invocaton is existing. This dict and `_invocationId2InstanceId` helps us remove an invocation by InvocationId.
        protected readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _serviceMapping = new();

        // This dict maps an InvocationId to the InstanceId which routed it. This dict and `_serviceMapping` helps us remove an invocation by InvocationId.
        protected readonly ConcurrentDictionary<string, string> _invocationId2InstanceId = new();

        public void AddServiceMappingMessage(ServiceMappingMessage serviceMappingMessage)
        {
            _serviceMapping.AddOrUpdate(
                serviceMappingMessage.InstanceId,
                new ConcurrentDictionary<string, bool>() { [serviceMappingMessage.InvocationId] = true },
                (key, invocationIdDict) =>
                {
                    if (!invocationIdDict.TryAdd(serviceMappingMessage.InvocationId, true))
                    {
                        throw new InvalidOperationException($"Failed to record a ServiceMappingMessage whose InvocationId '{serviceMappingMessage.InvocationId}' already exists");
                    }
                    return invocationIdDict;
                });
            if (!_invocationId2InstanceId.TryAdd(serviceMappingMessage.InvocationId, serviceMappingMessage.InstanceId))
            {
                throw new InvalidOperationException($"Failed to record a ServiceMappingMessage whose InvocationId '{serviceMappingMessage.InvocationId}' already exists");
            }
        }

        public void RemoveServiceMappingMessage(string invocationId)
        {
            if (_invocationId2InstanceId.TryGetValue(invocationId, out var instanceId))
            {
                if (_serviceMapping.TryGetValue(instanceId, out var invocationIdDict))
                {
                    invocationIdDict.TryRemove(invocationId, out _);
                }
            }
            _invocationId2InstanceId.TryRemove(invocationId, out _);
        }

        public abstract void CleanupInvocations(string instanceId);

        public abstract bool TryCompleteResult(string connectionId, CompletionMessage message);

        public abstract bool TryGetInvocationReturnType(string invocationId, out Type type);

    }
}
