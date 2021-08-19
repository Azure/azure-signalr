// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpointProvider : IServiceEndpointProvider
    {
        public static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {Constants.Keys.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IAppBuilder.RunAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private const string ClientPath = "aspnetclient";
        private const string ServerPath = "aspnetserver";

        private readonly string _endpoint;
        private readonly string _serverEndpoint;
        private readonly string _clientEndpoint;
        private readonly AccessKey _accessKey;
        private readonly string _appName;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly AccessTokenAlgorithm _algorithm;

        public IWebProxy Proxy { get; }

        public ServiceEndpointProvider(
            ServiceEndpoint endpoint,
            ServiceOptions options)
        {
            _accessTokenLifetime = options.AccessTokenLifetime;

            // Version is ignored for aspnet signalr case
            _endpoint = endpoint.Endpoint;
            _serverEndpoint = endpoint.ServerEndpoint;
            _clientEndpoint = endpoint.ClientEndpoint;
            _accessKey = endpoint.AccessKey;
            _appName = options.ApplicationName;
            _algorithm = options.AccessTokenAlgorithm;

            Proxy = options.Proxy;
        }

        private string GetPrefixedHubName(string applicationName, string hubName)
        {
            return string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
        }

        public Task<string> GenerateClientAccessTokenAsync(string hubName = null, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            var audience = $"{_endpoint}/{ClientPath}";
            return _accessKey.GenerateAccessTokenAsync(audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
        }

        public Task<string> GenerateServerAccessTokenAsync(string hubName, string userId, TimeSpan? lifetime = null)
        {
            if (_accessKey is AadAccessKey key)
            {
                return key.GenerateAadTokenAsync();
            }

            IEnumerable<Claim> claims = null;
            if (userId != null)
            {
                claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };
            }

            var audience = $"{_endpoint}/{ServerPath}/?hub={GetPrefixedHubName(_appName, hubName)}";
            return _accessKey.GenerateAccessTokenAsync(audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
        }

        public string GetClientEndpoint(string hubName = null, string originalPath = null, string queryString = null)
        {
            var uriBuilder = new UriBuilder(_clientEndpoint)
            {
                Path = ClientPath
            };

            var queryBuilder = new StringBuilder(queryString?.TrimStart('?'));

            if (!string.IsNullOrEmpty(originalPath))
            {
                if (queryBuilder.Length != 0)
                {
                    queryBuilder.Append("&");
                }
                queryBuilder
                    .Append(Constants.QueryParameter.OriginalPath)
                    .Append("=")
                    .Append(WebUtility.UrlEncode(originalPath));
            }
            uriBuilder.Query = queryBuilder.ToString();
            return uriBuilder.Uri.AbsoluteUri;
        }

        public string GetServerEndpoint(string hubName)
        {
            var uriBuilder = new UriBuilder(_serverEndpoint)
            {
                Path = $"{ServerPath}/",
                Query = $"hub={GetPrefixedHubName(_appName, hubName)}"
            };
            return uriBuilder.Uri.AbsoluteUri;
        }
    }
}
