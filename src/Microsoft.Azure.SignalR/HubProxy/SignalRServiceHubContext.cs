// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class SignalRServiceHubContext<THub> : IHubContext<THub> where THub : Hub
    {
        public SignalRServiceHubContext(IConnectionServiceProvider connectionServiceProvider, IHubMessageSender hubMessageSender)
        {
            Clients = new HubClientsProxy(hubMessageSender, connectionServiceProvider.GetEndpoint(), connectionServiceProvider.GetAccessToken(), typeof(THub).Name);
            Groups = new GroupManagerProxy(hubMessageSender, connectionServiceProvider.GetEndpoint(), connectionServiceProvider.GetAccessToken(), typeof(THub).Name);
        }

        public IHubClients Clients { get; }

        public IGroupManager Groups { get; }
    }
}
