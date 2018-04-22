// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceHubContext : IHubContext<Hub>
    {
        public ServiceHubContext(string hubName, IServiceEndpointUtility serviceEndpointUtility,
            IHubMessageSender hubMessageSender)
        {
            var endpoint = serviceEndpointUtility.Endpoint;
            var accessKey = serviceEndpointUtility.AccessKey;
            Clients = new HubClientsProxy(hubMessageSender, endpoint, accessKey, hubName);
            Groups = new GroupManagerProxy(hubMessageSender, endpoint, accessKey, hubName);
        }

        public IHubClients Clients { get; }

        public IGroupManager Groups { get; }
    }
}
