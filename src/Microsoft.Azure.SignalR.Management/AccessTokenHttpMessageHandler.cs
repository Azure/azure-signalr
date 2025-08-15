// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    private readonly RestApiAccessTokenGenerator _accessTokenGenerator;
    private readonly string _audienceBaseUri;

    public AccessTokenHttpMessageHandler(IServiceEndpointManager serviceEndpointManager, IServerNameProvider serverNameProvider)
    {
        var serviceEndpoint = serviceEndpointManager.Endpoints.Keys.First();
        _accessTokenGenerator = new RestApiAccessTokenGenerator(serviceEndpoint.AccessKey, serverNameProvider.GetName());
        _audienceBaseUri = serviceEndpoint.AudienceBaseUrl;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var audience = $"{_audienceBaseUri}{request.RequestUri!.AbsolutePath.TrimStart('/')}";
        var token = await _accessTokenGenerator.Generate(audience, Constants.Periods.DefaultAccessTokenLifetime);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
