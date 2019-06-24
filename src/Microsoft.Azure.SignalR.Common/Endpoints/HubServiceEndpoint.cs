﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    internal class HubServiceEndpoint : ServiceEndpoint
    {
        public HubServiceEndpoint(string hub, IServiceEndpointProvider provider, ServiceEndpoint endpoint) : base(endpoint)
        {
            Hub = hub;
            Provider = provider;
        }

        internal HubServiceEndpoint() : base() { }

        public string Hub { get; }

        public IServiceEndpointProvider Provider { get; }
    }
}
