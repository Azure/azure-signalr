// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class ClientInvocationManager: IClientInvocationManager
    {
        public ICallerClientResultsManager Caller { get; }
        public IRoutedClientResultsManager Router { get; }

        ClientInvocationManager(IHubProtocolResolver hubProtocolResolver)
        {
            Caller = new CallerClientResultsManager(hubProtocolResolver);
            Router = new RoutedClientResultsManager();
        }
    }
}