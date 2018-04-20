// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceHubContext: IHubContext<Hub>
    {
        public ServiceHubContext(IConnectionServiceProvider connectionServiceProvider, IHubMessageSender hubMessageSender, string hubName)
        {
            Clients = new HubClientsProxy(hubMessageSender, connectionServiceProvider.GetEndpoint(), connectionServiceProvider.GetAccessToken(), hubName);
            Groups = new GroupManagerProxy(hubMessageSender, connectionServiceProvider.GetEndpoint(), connectionServiceProvider.GetAccessToken(), hubName);
        }

        public IHubClients Clients { get; }

        public IGroupManager Groups { get; }
    }
}
