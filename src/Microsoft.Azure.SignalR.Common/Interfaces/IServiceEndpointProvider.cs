// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointProvider
    {
        string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null, string requestId = null);

        string GetClientEndpoint(string hubName, string originalPath, string queryString);

        string GenerateServerAccessToken(string hubName, string userId, TimeSpan? lifetime = null, string requestId = null);

        string GetServerEndpoint(string hubName);

        IWebProxy Proxy { get; }

    }
}
