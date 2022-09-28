// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal sealed class ClientInvocationManager : IClientInvocationManager
    {
        public ICallerClientResultsManager Caller { get;  }
        public IRoutedClientResultsManager Router { get; }

        public ClientInvocationManager(IHubProtocolResolver hubProtocolResolver)
        {
            Caller = new CallerClientResultsManager(hubProtocolResolver);
            Router = new RoutedClientResultsManager();
        }
    }
}
