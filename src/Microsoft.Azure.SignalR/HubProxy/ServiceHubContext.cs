// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceHubContext : IHubContext<Hub>
    {
        public ServiceHubContext(string hubName, IHubMessageSender hubMessageSender)
        {
            Clients = new HubClientsProxy(hubMessageSender, hubName);
            Groups = new GroupManagerProxy(hubMessageSender, hubName);
        }

        public IHubClients Clients { get; }

        public IGroupManager Groups { get; }
    }
}
