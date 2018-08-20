// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class NegotiateHandler
    {
        private readonly IServiceEndpointProvider _endpointProvider;
        private readonly bool _isDefaultUserIdProvider;
        private readonly Func<HttpContext, IEnumerable<Claim>> _claimsProvider;

        public NegotiateHandler(IServiceEndpointProvider endpointProvider, IUserIdProvider userIdProvider, IOptions<ServiceOptions> options)
        {
            _endpointProvider = endpointProvider ?? throw new ArgumentNullException(nameof(endpointProvider));
            _isDefaultUserIdProvider = userIdProvider is DefaultUserIdProvider;
            _claimsProvider = options?.Value?.ClaimsProvider;
        }

        public NegotiationResponse Process(HttpContext context, string hubName)
        {
            var claims = BuildClaims(context);
            return new NegotiationResponse
            {
                Url = _endpointProvider.GetClientEndpoint(hubName),
                AccessToken = _endpointProvider.GenerateClientAccessToken(hubName, claims),
                // Need to set this even though it's technically protocol violation https://github.com/aspnet/SignalR/issues/2133
                AvailableTransports = new List<AvailableTransport>()
            };
        }

        private IEnumerable<Claim> BuildClaims(HttpContext context)
        {
            var claims = _claimsProvider?.Invoke(context) ?? context.User.Claims;

            if (_isDefaultUserIdProvider)
            {
                return claims;
            }

            // Add an empty user Id claim to tell service that user has a custom IUserIdProvider.
            var customUserIdClaim = new Claim(Constants.ClaimType.UserId, string.Empty);
            if (claims == null)
            {
                return new[] {customUserIdClaim};
            }
            else
            {
                return new List<Claim>(claims) {customUserIdClaim};
            }
        }
    }
}
