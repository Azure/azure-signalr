// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpointProvider : IServiceEndpointProvider
    {
        private const string ClientPath = "aspnetclient";
        private const string ServerPath = "aspnetserver";

        private static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {ServiceOptions.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IAppBuilder.RunAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly int? _port;
        private readonly TimeSpan _accessTokenLifetime;

        public ServiceEndpointProvider(ServiceOptions options)
        {
            var connectionString = options.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(ConnectionStringNotFound);
            }

            _accessTokenLifetime = options.AccessTokenLifetime;

            // Version is ignored for aspnet signalr case
            (_endpoint, _accessKey, _, _port) = ConnectionStringParser.Parse(connectionString);
        }

        public string GenerateClientAccessToken(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            var audience = $"{_endpoint}/{ClientPath}";
            return InternalGenerateAccessToken(audience, claims, lifetime ?? _accessTokenLifetime);
        }

        public string GenerateServerAccessToken(string hubName, string userId, TimeSpan? lifetime = null)
        {
            IEnumerable<Claim> claims = null;
            if (userId != null)
            {
                claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };
            }

            var audience = $"{_endpoint}/{ServerPath}/?hub={hubName.ToLower()}";

            return InternalGenerateAccessToken(audience, claims, lifetime ?? _accessTokenLifetime);
        }

        public string GetClientEndpoint()
        {
            return _port.HasValue ?
                $"{_endpoint}:{_port}/{ClientPath}" :
                $"{_endpoint}/{ClientPath}";
        }

        public string GetServerEndpoint(string hubName)
        {
            return _port.HasValue ?
                $"{_endpoint}:{_port}/{ServerPath}/?hub={hubName.ToLower()}" :
                $"{_endpoint}/{ServerPath}/?hub={hubName.ToLower()}";
        }

        private string InternalGenerateAccessToken(string audience, IEnumerable<Claim> claims, TimeSpan lifetime)
        {
            var expire = DateTime.UtcNow.Add(lifetime);

            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: claims,
                expires: expire,
                signingKey: _accessKey
            );
        }
    }
}
