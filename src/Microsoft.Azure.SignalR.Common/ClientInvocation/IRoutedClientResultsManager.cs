// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IRoutedClientResultsManager
    {
        Task<object> AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken);

        bool TryRemoveRoutedInvocation(string invocationId, out RoutedInvocation routedInvocation);

        bool TryGetRoutedInvocation(string invocationId, out RoutedInvocation routedInvocation);

        bool TryGetInvocationReturnType(string invocationId, out Type type);

    }

    internal record RoutedInvocation(string ConnectionId, string CallerServerId, object Tcs, Action<object, CompletionMessage> Complete)
    {
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