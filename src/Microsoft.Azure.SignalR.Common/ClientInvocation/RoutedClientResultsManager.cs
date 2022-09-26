// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class RoutedClientResultsManager : IRoutedClientResultsManager
    {
        public Task<object> AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool TryCompleteResult(string connectionId, CompletionMessage message)
        {
            throw new NotImplementedException();
        }

        public bool TryGetRoutedInvocation(string invocationId, out RoutedInvocation routedInvocation)
        {
            throw new NotImplementedException();
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            throw new NotImplementedException();
        }

        public void CleanupInvocations(string instanceId)
        {
            throw new NotImplementedException();
        }
    }
}