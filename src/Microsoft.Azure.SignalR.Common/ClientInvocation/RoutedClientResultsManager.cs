// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class RoutedClientResultsManager : IRoutedClientResultsManager
    {
        public Task<object> AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public bool CheckRoutedInvocation(string invocationId)
        {
            throw new NotImplementedException();
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            throw new NotImplementedException();
        }

        private record RoutedInvocation(string ConnectionId, string CallerServerId, object Tcs, Action<object, CompletionMessage> Complete)
        {

        }

    }
}
#else
namespace Microsoft.Azure.SignalR
{ 
    internal class RoutedClientResultsManager: IRoutedClientResultsManager
    {

    }
}
#endif