// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IRoutedClientResultsManager
    {
        void AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, string instanceId, CancellationToken cancellationToken);

        bool TryCompleteResult(string connectionId, CompletionMessage message);

        bool TryGetInvocationReturnType(string invocationId, out Type type);

        bool CheckRoutedInvocation(string invocationId);

        void CleanupInvocations(string instanceId);
    }
}