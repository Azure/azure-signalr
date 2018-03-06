﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class HubProxy
    {
        public HubProxy(string endpoint, string accessKey, string hubName) : this(endpoint, accessKey, hubName, null)
        {
        }

        public HubProxy(string endpoint, string accessKey, string hubName, HubProxyOptions options)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (string.IsNullOrEmpty(accessKey))
            {
                throw new ArgumentNullException(nameof(accessKey));
            }

            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            Clients = new HubClientsProxy(endpoint, accessKey, hubName, options);
            Groups = new GroupManagerProxy(endpoint, accessKey, hubName, options);

            HubName = hubName.ToLower();
        }

        public string HubName { get; }

        public IHubClients<IClientProxy> Clients { get; }

        public IGroupManager Groups { get; }
    }
}
