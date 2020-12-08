// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR.Management
{
    internal class NegotiateProcessor
    {
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly IEndpointRouter _router;
        private readonly MultiEndpointConnectionContainerFactory _endpointsContainerFactory;

        public NegotiateProcessor(IServiceEndpointManager serviceEndpointManager, IEndpointRouter router, MultiEndpointConnectionContainerFactory endpointsContainerFactory)
        {
            _serviceEndpointManager = serviceEndpointManager;
            _router = router;
            _endpointsContainerFactory = endpointsContainerFactory;
        }

        public async Task<NegotiationResponse> GetClientEndpointAsync(string hubName, HttpContext httpContext = null, string userId = null, IEnumerable<Claim> claims = null, TimeSpan? lifeTime = null, CancellationToken cancellationToken = default)
        {
            if (cancellationToken == default && httpContext != null)
            {
                cancellationToken = httpContext.RequestAborted;
            }

            var container = _endpointsContainerFactory.GetOrCreate(hubName).Value;
            //ensure connections to each endpoint are initialized, so that the online status of endpoints are valid
            await container.ConnectionInitializedTask.OrTimeout(cancellationToken);

            var candidateEndpoints = _serviceEndpointManager.GetEndpoints(hubName);
            var selectedEndpoint = _router.GetNegotiateEndpoint(httpContext, candidateEndpoints);
            var provider = _serviceEndpointManager.GetEndpointProvider(selectedEndpoint);

            userId ??= httpContext?.User?.Identity?.Name;
            claims ??= httpContext?.User?.Claims;
            var claimsWithUserId = ClaimsUtility.BuildJwtClaims(null, userId: userId, () => claims);

            var tokenTask = provider.GenerateClientAccessTokenAsync(hubName, claimsWithUserId, lifeTime);
            await tokenTask.OrTimeout(cancellationToken);
            return new NegotiationResponse
            {
                Url = provider.GetClientEndpoint(hubName, null, null),
                AccessToken = tokenTask.Result
            };
        }
    }
}