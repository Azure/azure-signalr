// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class TokenProvider
    {
        public static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromSeconds(30);

        private readonly EndpointProvider _endpointProvider;
        private readonly string _accessKey;
        private TimeSpan? _lifetime;

        public DateTime ExpireTime => DateTime.UtcNow.Add(TokenLifetime);

        public TimeSpan TokenLifetime {
            get => _lifetime ?? DefaultAccessTokenLifetime;
            set => _lifetime = value;
        }

        public TokenProvider(string endpoint, string accessKey)
            : this(new EndpointProvider(endpoint), accessKey, null)
        {
        }

        public TokenProvider(string endpoint, string accessKey, TimeSpan lifetime)
            : this(new EndpointProvider(endpoint), accessKey, lifetime)
        {
        }

        public TokenProvider(EndpointProvider endpointProvider, string accessKey)
            : this(endpointProvider, accessKey, null)
        {
        }

        public TokenProvider(EndpointProvider endpointProvider, string accessKey, TimeSpan? lifetime)
        {
            if (string.IsNullOrEmpty(accessKey))
            {
                throw new ArgumentNullException(nameof(accessKey));
            }

            _endpointProvider = endpointProvider ?? throw new ArgumentNullException(nameof(endpointProvider));
            _accessKey = accessKey;
            _lifetime = lifetime;
        }

        public string GenerateClientAccessToken<THub>(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
            where THub : Hub
        {
            return GenerateClientAccessToken(typeof(THub).Name, claims, lifetime);
        }

        public string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null,
            TimeSpan? lifetime = null)
        {
            var expire = lifetime.HasValue ? DateTime.UtcNow.Add(lifetime.Value) : ExpireTime;
            return AuthenticationHelper.GenerateJwtBearer(
                audience: _endpointProvider.GetClientEndpoint(hubName),
                claims: claims,
                expires: expire,
                signingKey: _accessKey
            );
        }

        public string GenerateServerAccessToken<THub>(TimeSpan? lifetime = null) where THub : Hub
        {
            return GenerateServerAccessToken(typeof(THub).Name, lifetime);
        }

        public string GenerateServerAccessToken(string hubName, TimeSpan? lifetime = null)
        {
            var expire = lifetime.HasValue ? DateTime.UtcNow.Add(lifetime.Value) : ExpireTime;
            return AuthenticationHelper.GenerateJwtBearer(
                audience: _endpointProvider.GetServerEndpoint(hubName),
                claims: null,
                expires: expire,
                signingKey: _accessKey
            );
        }
    }
}
