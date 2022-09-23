// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Buffers;
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

         void TryCompleteResultFromSerializedMessage(string connectionId, string protocol, ReadOnlySequence<byte> message);

         void TryCompleteResult(string connectionId, CompletionMessage message);

         bool TryRemoveInvocation(string invocationId, out PendingInvocation invocation);

         bool TryGetInvocationReturnType(string invocationId, out Type type);
    }

    record PendingInvocation(Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)
    {

    }
}
#else
namespace Microsoft.Azure.SignalR
{
    internal interface IClientResultsManager
    {
    }
}
#endif