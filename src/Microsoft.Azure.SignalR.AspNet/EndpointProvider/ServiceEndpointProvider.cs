// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet;

internal class ServiceEndpointProvider : IServiceEndpointProvider
{
    public static readonly string ConnectionStringNotFound =
        "No connection string was specified. " +
        $"Please specify a configuration entry for {Constants.Keys.ConnectionStringDefaultKey}, " +
        "or explicitly pass one using IAppBuilder.RunAzureSignalR(connectionString) in Startup.ConfigureServices.";

    private const string ClientPath = "aspnetclient";

    private const string ServerPath = "aspnetserver";

    private readonly string _audienceBaseUrl;

    private readonly string _clientEndpoint;

    private readonly string _serverEndpoint;

    private readonly IAccessKey _accessKey;

    private readonly string _appName;

    private readonly TimeSpan _accessTokenLifetime;

    private readonly AccessTokenAlgorithm _algorithm;

    public IWebProxy Proxy { get; }

    public ServiceEndpointProvider(ServiceEndpoint endpoint, ServiceOptions options)
    {
        _accessTokenLifetime = options.AccessTokenLifetime;

        // Version is ignored for aspnet signalr case
        _audienceBaseUrl = endpoint.AudienceBaseUrl;
        _clientEndpoint = endpoint.ClientEndpoint.AbsoluteUri;
        _serverEndpoint = endpoint.ServerEndpoint.AbsoluteUri;
        _accessKey = endpoint.AccessKey;
        _appName = options.ApplicationName;
        _algorithm = options.AccessTokenAlgorithm;

        Proxy = options.Proxy;
    }

    public Task<string> GenerateClientAccessTokenAsync(string hubName = null, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
    {
        var audience = $"{_audienceBaseUrl}{ClientPath}";

        return _accessKey.GenerateAccessTokenAsync(audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
    }

    public string GetClientEndpoint(string hubName = null, string originalPath = null, string queryString = null)
    {
        var queryBuilder = new StringBuilder();

        if (!string.IsNullOrEmpty(queryString))
        {
            queryBuilder.Append(queryString);
        }

        if (!string.IsNullOrEmpty(originalPath))
        {
            if (queryBuilder.Length == 0)
            {
                queryBuilder.Append("?");
            }
            else
            {
                queryBuilder.Append("&");
            }

            queryBuilder
                .Append(Constants.QueryParameter.OriginalPath)
                .Append("=")
                .Append(WebUtility.UrlEncode(originalPath));
        }

        return $"{_clientEndpoint}{ClientPath}{queryBuilder}";
    }

    public string GetServerEndpoint(string hubName)
    {
        return $"{_serverEndpoint}{ServerPath}/?hub={GetPrefixedHubName(_appName, hubName)}";
    }

    public IAccessTokenProvider GetServerAccessTokenProvider(string hubName, string serverId)
    {
        if (_accessKey is MicrosoftEntraAccessKey key)
        {
            return new MicrosoftEntraTokenProvider(key);
        }
        else if (_accessKey is AccessKey key2)
        {
            var audience = $"{_audienceBaseUrl}{ServerPath}/?hub={GetPrefixedHubName(_appName, hubName)}";
            var claims = serverId != null ? new[] { new Claim(ClaimTypes.NameIdentifier, serverId) } : null;
            return new LocalTokenProvider(key2, audience, claims, _algorithm, _accessTokenLifetime);
        }
        else
        {
            throw new ArgumentNullException(nameof(AccessKey));
        }
    }

    private string GetPrefixedHubName(string applicationName, string hubName)
    {
        return string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
    }
}
