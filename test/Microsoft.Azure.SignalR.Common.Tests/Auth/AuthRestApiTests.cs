// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

public class AuthRestApiTests
{
    public static readonly Regex CommonAnnotatedKeyRegex = new("[A-Za-z0-9]{52}JQQJ9(9|D)[A-Za-z0-9][A-L][A-Za-z0-9]{16}[A-Za-z][A-Za-z0-9]{7}([A-Za-z0-9]{2}==)?",
                                                               RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string Endpoint = "provide a valid endpoint";

    [Fact(Skip = "local test only")]
    public async Task TestGetAccessKey()
    {
        var key = await GetAccessKeyAsync(Endpoint, default);
        Assert.StartsWith("t-", key.KeyId);
        Assert.Matches(CommonAnnotatedKeyRegex, key.AccessKey);
    }

    [Fact(Skip = "local test only")]
    public async Task TestGetClientToken()
    {
        var token = await GetClientTokenAsync(Endpoint, "foo", default);
        Assert.NotNull(token);
    }

    private static async Task<AccessKeyResponse> GetAccessKeyAsync(string endpoint, CancellationToken ctoken)
    {
        var credential = new VisualStudioCredential();
        var tokenRequest = new TokenRequestContext([Constants.AsrsDefaultScope]);
        var token = await credential.GetTokenAsync(tokenRequest, ctoken);

        var uri = $"{endpoint}/api/v1/auth/accessKey";
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

        var httpClient = HttpClientFactory.Instance.CreateClient();
        var response = await httpClient.SendAsync(request, ctoken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(ctoken);
        return JsonSerializer.Deserialize<AccessKeyResponse>(content);
    }

    private static async Task<ClientTokenResponse> GetClientTokenAsync(string endpoint, string hub, CancellationToken ctoken)
    {
        var credential = new VisualStudioCredential();
        var tokenRequest = new TokenRequestContext([Constants.AsrsDefaultScope]);
        var token = await credential.GetTokenAsync(tokenRequest, ctoken);

        var uri = $"{endpoint}/api/hubs/{hub}/:generateToken";
        var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

        var httpClient = HttpClientFactory.Instance.CreateClient();
        var response = await httpClient.SendAsync(request, ctoken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync(ctoken);
        return JsonSerializer.Deserialize<ClientTokenResponse>(content);
    }
}
