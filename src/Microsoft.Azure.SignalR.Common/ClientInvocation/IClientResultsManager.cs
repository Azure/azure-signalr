// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientResultsManager
    {
        string GetNewInvocationId(string connectionId, string serverGUID);

        void AddServiceMappingMessage(string invocationId, ServiceMappingMessage serviceMappingMessage);

        void CleanupInvocations(string instanceId);

        Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken);

        bool TryCompleteResultFromSerializedMessage(string connectionId, string protocol, ReadOnlySequence<byte> message);

        bool TryCompleteResult(string connectionId, CompletionMessage message);

        bool TryRemoveInvocation(string invocationId, out PendingInvocation invocation);

        bool TryGetInvocationReturnType(string invocationId, out Type type);
    }

    internal record PendingInvocation(Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)
    {
    }
}