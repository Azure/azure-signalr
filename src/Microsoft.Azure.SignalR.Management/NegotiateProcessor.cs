// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.Management
{
    internal class NegotiateProcessor
    {
        private const string GeneratingTokenTaskDescription = "Generating client access token task";
        private const string ErrorMsg = "Geting client endpoint operation was not completed successfully.";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly IEndpointRouter _router;

        public NegotiateProcessor(IServiceEndpointManager serviceEndpointManager, IEndpointRouter router)
        {
            _serviceEndpointManager = serviceEndpointManager;
            _router = router;
        }

        public async Task<NegotiationResponse> NegotiateAsync(string hubName, HttpContext httpContext = null, string userId = null, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (cancellationToken == default && httpContext != null)
                {
                    cancellationToken = httpContext.RequestAborted;
                }

                var candidateEndpoints = _serviceEndpointManager.GetEndpoints(hubName);
                var selectedEndpoint = _router.GetNegotiateEndpoint(httpContext, candidateEndpoints);
                var provider = _serviceEndpointManager.GetEndpointProvider(selectedEndpoint);

                Func<IEnumerable<Claim>> claimProvider = null;
                if (claims != null)
                {
                    claimProvider = () => claims;
                }
                var claimsWithUserId = ClaimsUtility.BuildJwtClaims(httpContext?.User, userId: userId, claimProvider);

                var tokenTask = provider.GenerateClientAccessTokenAsync(hubName, claimsWithUserId, lifetime);
                await tokenTask.OrTimeout(cancellationToken, Timeout, GeneratingTokenTaskDescription);
                return new NegotiationResponse
                {
                    Url = provider.GetClientEndpoint(hubName, null, null),
                    AccessToken = tokenTask.Result
                };
            }
            catch (Exception e)
            {
                throw new AzureSignalRException(ErrorMsg, e);
            }
        }
    }
}