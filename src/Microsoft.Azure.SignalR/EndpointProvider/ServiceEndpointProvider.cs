// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointProvider : IServiceEndpointProvider
    {
        private const string PreviewVersion = "1.0-preview";

        private static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {ServiceOptions.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly IServiceEndpointGenerator _generator;

        public ServiceEndpointProvider(IOptions<ServiceOptions> options)
        {
            var connectionString = options.Value.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(ConnectionStringNotFound);
            }

            _accessTokenLifetime = options.Value.AccessTokenLifetime;

            string version;
            int? port;
            (_endpoint, _accessKey, version, port) = ConnectionStringParser.Parse(connectionString);

            if (version == null || version == PreviewVersion)
            {
                _generator = new PreviewServiceEndpointGenerator(_endpoint, _accessKey);
            }
            else
            {
                _generator = new DefaultServiceEndpointGenerator(_endpoint, _accessKey, version, port);
            }
        }

        public string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null, string requestId = null)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetClientAudience(hubName);

            return AuthenticationHelper.GenerateAccessToken(_accessKey, audience, claims, lifetime ?? _accessTokenLifetime, requestId);
        }

        public string GenerateServerAccessToken(string hubName, string userId, TimeSpan? lifetime = null, string requestId = null)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetServerAudience(hubName);
            var claims = userId != null ? new[] {new Claim(ClaimTypes.NameIdentifier, userId)} : null;

            return AuthenticationHelper.GenerateAccessToken(_accessKey, audience, claims, lifetime ?? _accessTokenLifetime, requestId);
        }

        public string GetClientEndpoint(string hubName, string originalPath, string queryString)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetClientEndpoint(hubName, originalPath, queryString);
        }

        public string GetServerEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetServerEndpoint(hubName);
        }
    }
}
