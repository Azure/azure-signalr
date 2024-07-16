// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointProvider : IServiceEndpointProvider
    {
        public static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {Constants.Keys.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private readonly AccessKey _accessKey;

        private readonly string _appName;

        private readonly TimeSpan _accessTokenLifetime;

        private readonly IServiceEndpointGenerator _generator;

        private readonly AccessTokenAlgorithm _algorithm;

        public IWebProxy Proxy { get; }

        public ServiceEndpointProvider(ServiceEndpoint endpoint, ServiceOptions serviceOptions)
        {
            _accessTokenLifetime = serviceOptions.AccessTokenLifetime;
            _accessKey = endpoint.AccessKey;
            _appName = serviceOptions.ApplicationName;
            _algorithm = serviceOptions.AccessTokenAlgorithm;

            Proxy = serviceOptions.Proxy;

            _generator = new DefaultServiceEndpointGenerator(endpoint);
        }

        public Task<string> GenerateClientAccessTokenAsync(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetClientAudience(hubName, _appName);

            return _accessKey.GenerateAccessTokenAsync(audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
        }

        public string GetClientEndpoint(string hubName, string originalPath, string queryString)
        {
            return string.IsNullOrEmpty(hubName)
                ? throw new ArgumentNullException(nameof(hubName))
                : _generator.GetClientEndpoint(hubName, _appName, originalPath, queryString);
        }

        public IAccessTokenProvider GetServerAccessTokenProvider(string hubName, string serverId)
        {
            if (_accessKey is AccessKeyForMicrosoftEntra key)
            {
                return new MicrosoftEntraTokenProvider(key);
            }
            else if (_accessKey is not null)
            {
                var audience = _generator.GetServerAudience(hubName, _appName);
                var claims = serverId != null ? new[] { new Claim(ClaimTypes.NameIdentifier, serverId) } : null;
                return new LocalTokenProvider(_accessKey, audience, claims, _algorithm, _accessTokenLifetime);
            }
            else
            {
                throw new NotSupportedException("Access key cannot be null.");
            }
        }

        public string GetServerEndpoint(string hubName)
        {
            return string.IsNullOrEmpty(hubName)
                ? throw new ArgumentNullException(nameof(hubName))
                : _generator.GetServerEndpoint(hubName, _appName);
        }
    }
}
