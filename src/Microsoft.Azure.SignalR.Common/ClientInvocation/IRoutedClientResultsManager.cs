// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal interface IRoutedClientResultsManager
    {
        public Task<object> AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken);

        public bool CheckRoutedInvocation(string invocationId);

        public bool TryGetInvocationReturnType(string invocationId, out Type type);

        private record RoutedInvocation;
    }
}
#else
namespace Microsoft.Azure.SignalR
{
    internal interface IRoutedClientResultsManager
    {
    }
}
#endif