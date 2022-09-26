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

        Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken);

        void AddServiceMappingMessage(string invocationId, ServiceMappingMessage serviceMappingMessage);

        bool TryRemoveInvocation(string invocationId, out PendingInvocation invocation);

        bool TryCompleteResult(string connectionId, CompletionMessage message);

        /// <summary>
        /// Try complete from <paramref name="message"/> which is serialized from a <see cref="CompletionMessage"/> in <paramref name="protocol"/>
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="protocol">Serialization protocol of <paramref name="message"/></param>
        /// <param name="message">the seriazliation result of a <see cref="CompletionMessage"/></param>
        /// <returns></returns>
        bool TryCompleteResultFromSerializedMessage(string connectionId, string protocol, ReadOnlySequence<byte> message);

        bool TryGetInvocationReturnType(string invocationId, out Type type);

        void CleanupInvocations(string instanceId);
    }

    internal record PendingInvocation(Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete)
    {
    }
}