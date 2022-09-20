// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System;
using Microsoft.Azure.SignalR.Protocol;
using System.Threading.Tasks;
using System.Threading;
using System.Buffers;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientResultsManager
    {
        public ulong GetNewInvocation();

        public void AddServiceMappingMessage(string instanceId, ServiceMappingMessage serviceMappingMessage);

        public void RemoveServiceMappingMessageWithOfflinePing(string instanceId);

        public Task<T> AddRoutedInvocation<T>(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken);

        public Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken);

        public void AddInvocation(string invocationId, (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Complete) invocationInfo);

        public void TryCompleteResult(string connectionId, string protocol, ReadOnlySequence<byte> message);

        public void TryCompleteResult(string connectionId, CompletionMessage message);

        public bool CheckRoutedInvocation(string invocationId);

        public void TryCompleteRoutedResult(string connectionId, CompletionMessage message);

        public (Type Type, string ConnectionId, object Tcs, Action<object, CompletionMessage> Completion)? RemoveInvocation(string invocationId);

        public (Type Type, string ConnectionId, string callerServerId, object Tcs, Action<object, CompletionMessage> Completion)? RemoveRoutedInvocation(string invocationId);

        public bool TryGetType(string invocationId, out Type type);

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