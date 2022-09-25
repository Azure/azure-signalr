// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class ClientInvocationManager
    {
        public IClientResultsManager Caller => _clientResultsManager;
        public IRoutedClientResultsManager Router => _routedClientResultsManager;

        private readonly IClientResultsManager _clientResultsManager;
        private readonly IRoutedClientResultsManager _routedClientResultsManager;

        public IHubProtocolResolver HubProtocolResolver { get; }

        public ClientInvocationManager(IHubProtocolResolver hubProtocolResolver)
        {
            _clientResultsManager = new ClientResultsManager(hubProtocolResolver);
            _routedClientResultsManager = new RoutedClientResultsManager();
        }
    }
}
