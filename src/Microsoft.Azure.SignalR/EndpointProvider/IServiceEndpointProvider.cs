// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointProvider
    {
        string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null);

        string GetClientEndpoint(string hubName, QueryString queryString);

        string GenerateServerAccessToken<THub>(string userId, TimeSpan? lifetime = null) where THub : Hub;

        string GetServerEndpoint<THub>() where THub : Hub;
    }
}
