// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class SignalRServiceContext
    {
        private IConnectionServiceProvider _connectionServiceProvider;

        internal SignalRServiceContext(IConnectionServiceProvider connectionServiceProvider, IHubContext<Hub> hubContext)
        {
            _connectionServiceProvider = connectionServiceProvider;
            HubContext = hubContext;
        }

        public IHubContext<Hub> HubContext { get; set; }

        public string GenerateEndpoint(string hubName)
        {
            return _connectionServiceProvider.GetClientEndpoint(hubName);
        }

        public string GenerateAccessToken(string hubName, IEnumerable<Claim> claims)
        {
            return _connectionServiceProvider.GenerateClientAccessToken(hubName, claims);
        }
    }
}
