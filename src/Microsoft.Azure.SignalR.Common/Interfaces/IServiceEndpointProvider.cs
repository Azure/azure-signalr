// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal interface IServiceEndpointProvider
{
    Task<string> GenerateClientAccessTokenAsync(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null);

    string GetClientEndpoint(string hubName, string originalPath, string queryString);

    IAccessTokenProvider GetServerAccessTokenProvider(string hubName, string serverId);

    string GetServerEndpoint(string hubName);

    IWebProxy Proxy { get; }
}
