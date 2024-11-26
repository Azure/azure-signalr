// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal sealed class DefaultServiceEndpointGenerator : IServiceEndpointGenerator
{
    private const string ClientPath = "client";

    private const string ServerPath = "server";

    public string? Version { get; }

    public string AudienceBaseUrl { get; }

    public string ClientEndpoint { get; }

    public string ServerEndpoint { get; }

    public DefaultServiceEndpointGenerator(ServiceEndpoint endpoint)
    {
        Version = endpoint.Version;
        AudienceBaseUrl = endpoint.AudienceBaseUrl;
        ClientEndpoint = endpoint.ClientEndpoint.AbsoluteUri;
        ServerEndpoint = endpoint.ServerEndpoint.AbsoluteUri;
    }

    public string GetClientAudience(string hubName, string applicationName) =>
        InternalGetUri(ClientPath, hubName, applicationName, AudienceBaseUrl);

    public string GetClientEndpoint(string hubName, string applicationName, string originalPath, string queryString)
    {
        var queryBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(originalPath))
        {
            queryBuilder.Append('&')
                .Append(Constants.QueryParameter.OriginalPath)
                .Append('=')
                .Append(WebUtility.UrlEncode(originalPath));
        }

        if (!string.IsNullOrEmpty(queryString))
        {
            queryBuilder.Append('&').Append(queryString);
        }

        return $"{InternalGetUri(ClientPath, hubName, applicationName, ClientEndpoint)}{queryBuilder}";
    }

    public string GetServerAudience(string hubName, string applicationName) =>
        InternalGetUri(ServerPath, hubName, applicationName, AudienceBaseUrl);

    public string GetServerEndpoint(string hubName, string applicationName) =>
        InternalGetUri(ServerPath, hubName, applicationName, ServerEndpoint);

    private static string GetPrefixedHubName(string applicationName, string hubName)
    {
        return string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
    }

    private static string InternalGetUri(string path, string hubName, string applicationName, string target)
    {
        return $"{target}{path}/?hub={GetPrefixedHubName(applicationName, hubName)}";
    }
}
