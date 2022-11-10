// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET7_0_OR_GREATER
using System;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal sealed class ClientInvocationManager : IClientInvocationManager
    {
        public ICallerClientResultsManager Caller { get; }
        public IRoutedClientResultsManager Router { get; }

        public ClientInvocationManager(IHubProtocolResolver hubProtocolResolver)
        {
            Caller = new CallerClientResultsManager(hubProtocolResolver ?? throw new ArgumentNullException(nameof(hubProtocolResolver)));
            Router = new RoutedClientResultsManager();
        }

        public void CleanupInvocationsByConnection(string connectionId)
        {
            Caller.CleanupInvocationsByConnection(connectionId);
            Router.CleanupInvocationsByConnection(connectionId);
        }

        public bool TryGetInvocationReturnType(string invocationId, out Type type)
        {
            if (Router.TryGetInvocationReturnType(invocationId, out type))
            {
                return true;
            }
            return Caller.TryGetInvocationReturnType(invocationId, out type);
        }
    }
}
#endif