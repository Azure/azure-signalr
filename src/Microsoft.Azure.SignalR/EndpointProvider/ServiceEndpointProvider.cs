// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointProvider : IServiceEndpointProvider, IDisposable
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

        private TimerAwaitable _timer = new TimerAwaitable(TimeSpan.Zero, TimeSpan.FromMinutes(55));

        public ServiceEndpointProvider(IServerNameProvider provider, ServiceEndpoint endpoint, ServiceOptions serviceOptions)
        {
            _accessTokenLifetime = serviceOptions.AccessTokenLifetime;
            _accessKey = endpoint.AccessKey;
            _appName = serviceOptions.ApplicationName;
            _algorithm = serviceOptions.AccessTokenAlgorithm;

            Proxy = serviceOptions.Proxy;

            var port = endpoint.Port;
            var version = endpoint.Version;

            _generator = new DefaultServiceEndpointGenerator(endpoint.Endpoint, version, port);

            if (endpoint.AccessKey is AadAccessKey key)
            {
                _ = UpdateAccessKeyAsync(endpoint, key, provider.GetName());
            }
        }

        private async Task UpdateAccessKeyAsync(ServiceEndpoint endpoint, AadAccessKey key, string serverId)
        {
            _timer.Start();
            while (await _timer)
            {
                _ = key.AuthorizeAsync(endpoint.Endpoint, endpoint.Port, serverId);
            }
        }

        public async Task<string> GenerateClientAccessTokenAsync(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetClientAudience(hubName, _appName);

            if (_accessKey is AadAccessKey key)
            {
                await key.AuthorizeTask;
            }
            return AuthUtility.GenerateAccessToken(_accessKey, audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
        }

        public async Task<string> GenerateServerAccessTokenAsync(string hubName, string userId, TimeSpan? lifetime = null)
        {
            if (_accessKey is AadAccessKey key)
            {
                return await key.GenerateAccessToken();
            }
            else
            {
                if (string.IsNullOrEmpty(hubName))
                {
                    throw new ArgumentNullException(nameof(hubName));
                }

                var audience = _generator.GetServerAudience(hubName, _appName);
                var claims = userId != null ? new[] { new Claim(ClaimTypes.NameIdentifier, userId) } : null;

                return AuthUtility.GenerateAccessToken(_accessKey, audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
            }
        }

        public string GetClientEndpoint(string hubName, string originalPath, string queryString)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetClientEndpoint(hubName, _appName, originalPath, queryString);
        }

        public string GetServerEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetServerEndpoint(hubName, _appName);
        }

        public void Dispose()
        {
            ((IDisposable)_timer).Dispose();
        }
    }
}
