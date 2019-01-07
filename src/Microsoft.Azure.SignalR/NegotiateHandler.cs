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
        private readonly IUserIdProvider _userIdProvider;
        private readonly Func<HttpContext, IEnumerable<Claim>> _claimsProvider;
        private readonly IServiceEndpointManager _endpointManager;
        private readonly IEndpointRouter _router;

        public NegotiateHandler(IServiceEndpointManager endpointManager, IEndpointRouter router, IUserIdProvider userIdProvider, IOptions<ServiceOptions> options)
        {
            _endpointManager = endpointManager ?? throw new ArgumentNullException(nameof(endpointManager));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _userIdProvider = userIdProvider ?? throw new ArgumentNullException(nameof(userIdProvider));
            _claimsProvider = options?.Value?.ClaimsProvider;
        }

        public NegotiationResponse Process(HttpContext context, string hubName)
        {
            var claims = BuildClaims(context);
            var request = context.Request;
            var originalPath = GetOriginalPath(request.Path);
            var provider = _endpointManager.GetEndpointProvider(_router.GetNegotiateEndpoint(_endpointManager.GetPrimaryEndpoints()));

            if (provider == null)
            {
                throw new InvalidOperationException("No endpoint available.");
            }

            return new NegotiationResponse
            {
                Url = provider.GetClientEndpoint(hubName, originalPath,
                    request.QueryString.HasValue ? request.QueryString.Value.Substring(1) : string.Empty),
                AccessToken = provider.GenerateClientAccessToken(hubName, claims),
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
    }
}
