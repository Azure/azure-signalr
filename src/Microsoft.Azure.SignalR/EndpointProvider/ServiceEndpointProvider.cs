﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointProvider : IServiceEndpointProvider
    {
        internal static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {Constants.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly IOptions<ServiceOptions> _serviceOptions;
        private readonly string _appName;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly IServiceEndpointGenerator _generator;

        public ServiceEndpointProvider(ServiceEndpoint endpoint, string appName = "", IOptions<ServiceOptions> serviceOptions = null, TimeSpan? ttl = null)
        {
            var connectionString = endpoint.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(ConnectionStringNotFound);
            }

            _accessTokenLifetime = ttl ?? Constants.DefaultAccessTokenLifetime;

            _endpoint = endpoint.Endpoint;
            _accessKey = endpoint.AccessKey;
            _serviceOptions = serviceOptions;
            _appName = appName;

            var port = endpoint.Port;
            var version = endpoint.Version;

            _generator = new DefaultServiceEndpointGenerator(_endpoint, _accessKey, version, port);
        }

        internal string ApplicationName
        {
            get
            {
                return _serviceOptions != null ? _serviceOptions.Value.ApplicationName : _appName;
            }
        }

        public string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null, string requestId = null)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetClientAudience(hubName, ApplicationName);

            return AuthenticationHelper.GenerateAccessToken(_accessKey, audience, claims, lifetime ?? _accessTokenLifetime, requestId);
        }

        public string GenerateServerAccessToken(string hubName, string userId, TimeSpan? lifetime = null, string requestId = null)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetServerAudience(hubName, ApplicationName);
            var claims = userId != null ? new[] {new Claim(ClaimTypes.NameIdentifier, userId)} : null;

            return AuthenticationHelper.GenerateAccessToken(_accessKey, audience, claims, lifetime ?? _accessTokenLifetime, requestId);
        }

        public string GetClientEndpoint(string hubName, string originalPath, string queryString)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetClientEndpoint(hubName, ApplicationName, originalPath, queryString);
        }

        public string GetServerEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetServerEndpoint(hubName, ApplicationName);
        }
    }
}
