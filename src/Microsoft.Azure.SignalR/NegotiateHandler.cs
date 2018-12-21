// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IUserIdProvider _userIdProvider;
        private readonly Func<HttpContext, IEnumerable<Claim>> _claimsProvider;

        public NegotiateHandler(IServiceEndpointProvider endpointProvider, IUserIdProvider userIdProvider, IOptions<ServiceOptions> options)
        {
            _endpointProvider = endpointProvider ?? throw new ArgumentNullException(nameof(endpointProvider));
            _userIdProvider = userIdProvider ?? throw new ArgumentNullException(nameof(userIdProvider));
            _claimsProvider = options?.Value?.ClaimsProvider;
        }

        public NegotiationResponse Process(HttpContext context, string hubName)
        {
            var claims = BuildClaims(context);
            var request = context.Request;
            var originalPath = GetOriginalPath(request.Path);

            string accessToken = null;
            try
            {
                accessToken = _endpointProvider.GenerateClientAccessToken(hubName, claims);
            }
            catch (ArgumentException)
            {
            }

            return new NegotiationResponse
            {
                Url = _endpointProvider.GetClientEndpoint(hubName, originalPath,
                    request.QueryString.HasValue ? request.QueryString.Value.Substring(1) : string.Empty),
                AccessToken = accessToken,
                // Need to set this even though it's technically protocol violation https://github.com/aspnet/SignalR/issues/2133
                AvailableTransports = new List<AvailableTransport>()
            };
        }

        private IEnumerable<Claim> BuildClaims(HttpContext context)
        {
            var userId = _userIdProvider.GetUserId(new ServiceHubConnectionContext(context));
            return ClaimsUtility.BuildJwtClaims(context.User, userId, GetClaimsProvider(context)).ToList();
        }

        private Func<IEnumerable<Claim>> GetClaimsProvider(HttpContext context)
        {
            if (_claimsProvider == null)
            {
                return null;
            }

            return () => _claimsProvider.Invoke(context);
        }

        private static string GetOriginalPath(string path)
        {
            path = path.TrimEnd('/');
            return path.EndsWith(Constants.Path.Negotiate)
                ? path.Substring(0, path.Length - Constants.Path.Negotiate.Length)
                : string.Empty;
        }

        private Claim[] GenerateClaims(int count)
        {
            var claims = new List<Claim>();
            while (count > 0)
            {
                var claimType = $"ClaimSubject{count}";
                var claimValue = $"ClaimValue{count}";
                claims.Add(new Claim(claimType, claimValue));
                count--;
            }
            return claims.ToArray();
        }
    }
}
