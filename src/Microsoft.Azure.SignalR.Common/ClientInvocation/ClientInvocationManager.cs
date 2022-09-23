// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
#if NET7_0_OR_GREATER
using Microsoft.AspNetCore.SignalR;
#endif

namespace Microsoft.Azure.SignalR
{
    internal class ClientInvocationManager
    {
        public IClientResultsManager Caller => _clientResultsManager;
        public IRoutedClientResultsManager Router => _routedClientResultsManager;

        private readonly IClientResultsManager _clientResultsManager;
        private readonly IRoutedClientResultsManager _routedClientResultsManager;

#if NET7_0_OR_GREATER
        public ClientInvocationManager(IHubProtocolResolver hubProtocolResolver)
        {
            _clientResultsManager = new ClientResultsManager(hubProtocolResolver);
            _routedClientResultsManager = new RoutedClientResultsManager();
        }
#else
        public ClientInvocationManager()
        {
            _clientResultsManager = new ClientResultsManager();
            _routedClientResultsManager = new RoutedClientResultsManager();
        }
#endif
    }
}