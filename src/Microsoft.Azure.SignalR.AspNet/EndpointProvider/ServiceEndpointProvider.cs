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
        private readonly AccessKey _accessKey;
        private readonly string _appName;
        private readonly int? _port;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly AccessTokenAlgorithm _algorithm;

        private readonly IServerNameProvider _provider;

        public IWebProxy Proxy { get; }

        public ServiceEndpointProvider(IServerNameProvider provider, ServiceEndpoint endpoint, ServiceOptions options)
        {
            _accessTokenLifetime = options.AccessTokenLifetime;

            // Version is ignored for aspnet signalr case
            _endpoint = endpoint.Endpoint;
            _accessKey = endpoint.AccessKey;
            _appName = options.ApplicationName;
            _port = endpoint.Port;
            _algorithm = options.AccessTokenAlgorithm;

            _provider = provider;

            Proxy = options.Proxy;
        }


        private string GetPrefixedHubName(string applicationName, string hubName)
        {
            return string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
        }

        public Task<string> GenerateClientAccessTokenAsync(string hubName = null, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            var audience = $"{_endpoint}/{ClientPath}";

            return _accessKey.GenerateAccessToken(audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
        }

        public Task<string> GenerateServerAccessTokenAsync(string hubName, string userId, TimeSpan? lifetime = null)
        {
            // if (_accessKey is AadAccessKey key)
            // {
            //     return key.GenerateAadToken();
            // }

            IEnumerable<Claim> claims = null;
            if (userId != null)
            {
                claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };
            }

            var audience = $"{_endpoint}/{ServerPath}/?hub={GetPrefixedHubName(_appName, hubName)}";

            return _accessKey.GenerateAccessToken(audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
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

            return _port.HasValue ?
                $"{_endpoint}:{_port}/{ClientPath}{queryBuilder}" :
                $"{_endpoint}/{ClientPath}{queryBuilder}";
        }

        public string GetServerEndpoint(string hubName)
        {
            return _port.HasValue ?
                $"{_endpoint}:{_port}/{ServerPath}/?hub={GetPrefixedHubName(_appName, hubName)}" :
                $"{_endpoint}/{ServerPath}/?hub={GetPrefixedHubName(_appName, hubName)}";
        }
    }
}
