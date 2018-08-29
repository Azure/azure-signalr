// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IServiceEndpointProvider
    {
        string GenerateServerAccessToken(string hubName, string userId, TimeSpan? lifetime = null);

        string GenerateClientAccessToken(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null);

        string GetServerEndpoint(string hubName);

        string GetClientEndpoint();
    }
}
