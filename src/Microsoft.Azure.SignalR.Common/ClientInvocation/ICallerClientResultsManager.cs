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
    internal interface ICallerClientResultsManager
    {
        string GenerateInvocationId(string connectionId);

        Task<T> AddInvocation<T>(string connectionId, string invocationId, CancellationToken cancellationToken);

        void AddServiceMappingMessage(ServiceMappingMessage serviceMappingMessage);

        bool TryCompleteResult(string connectionId, CompletionMessage message);

        public bool TryCompleteResult(string connectionId, ClientCompletionMessage message);

        bool TryGetInvocationReturnType(string invocationId, out Type type);

        void CleanupInvocations(string instanceId);
    }
}