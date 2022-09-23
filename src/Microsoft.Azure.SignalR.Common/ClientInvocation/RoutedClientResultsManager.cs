// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET7_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal class RoutedClientResultsManager: IRoutedClientResultsManager
    {
        private readonly ConcurrentDictionary<string,  RoutedInvocation> _routedInvocations = new();

        public Task<object> AddRoutedInvocation(string connectionId, string invocationId, string callerServerId, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>();
            
            var result = _routedInvocations.TryAdd(invocationId, new RoutedInvocation(connectionId, callerServerId, tcs, static (state, completionMessage) =>
            {
                var tcs = (TaskCompletionSource<object>)state;
                if (completionMessage.HasResult)
                {
                    tcs.SetResult(completionMessage.Result);
                }
                else
                {
                    tcs.SetException(new Exception(completionMessage.Error));
                }
            }
            ));
            Debug.Assert(result);
            cancellationToken.Register(static o =>
            {
                var tcs = (TaskCompletionSource<object>)o!;
                tcs.SetCanceled();
            }, tcs);

            return tcs.Task;
        }

        public bool TryGetRoutedInvocation(string invocationId, out RoutedInvocation routedInvocation)
        {
            return _routedInvocations.TryGetValue(invocationId, out routedInvocation);
        }

        public bool TryRemoveRoutedInvocation(string invocationId, out RoutedInvocation routedInvocation)
        {
            return _routedInvocations.TryRemove(invocationId, out routedInvocation);
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            if (_routedInvocations.TryGetValue(invocationId, out var item))
            {
                type = typeof(object);
                return true;
            }
            type = null;
            return false;
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