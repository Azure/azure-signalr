// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

/// <summary>
/// Appends access token for each HTTP request
/// </summary>
internal class AccessTokenHttpMessageHandler : DelegatingHandler
{
    // Internal for testing purposes
    internal static readonly string ServerName = $"{Environment.MachineName}_{Guid.NewGuid():N}";

    private readonly RestApiAccessTokenGenerator _accessTokenGenerator;

    public AccessTokenHttpMessageHandler(IServiceEndpointManager serviceEndpointManager)
    {
        var endpoint = serviceEndpointManager.Endpoints.Keys.First();
        _accessTokenGenerator = new RestApiAccessTokenGenerator(endpoint.AccessKey, ServerName);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var audience = request.RequestUri!.GetLeftPart(UriPartial.Path);
        var token = await _accessTokenGenerator.Generate(audience, Constants.Periods.DefaultAccessTokenLifetime);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
