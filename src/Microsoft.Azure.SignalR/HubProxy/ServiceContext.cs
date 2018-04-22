// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class ServiceContext
    {
        private readonly string _hubName;
        private readonly IServiceEndpointUtility _serviceEndpointUtility;

        internal ServiceContext(string hubName, IServiceEndpointUtility serviceEndpointUtility, IHubContext<Hub> hubContext)
        {
            _hubName = hubName;
            _serviceEndpointUtility = serviceEndpointUtility;
            HubContext = hubContext;
        }

        public IHubContext<Hub> HubContext { get; }

        public string GetEndpoint()
        {
            return _serviceEndpointUtility.GetClientEndpoint(_hubName);
        }

        public string GenerateAccessToken(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            return _serviceEndpointUtility.GenerateClientAccessToken(_hubName, claims, lifetime);
        }
    }
}
