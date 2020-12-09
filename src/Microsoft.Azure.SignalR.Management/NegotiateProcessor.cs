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
        private const string ConnectionInitializedTaskDescription = "Waiting for connection initialized task";
        private const string GeneratingTokenTaskDescription = "Generating client access token task";
        private const string ErrorMsg = "Geting client endpoint operation was not completed successfully.";
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly IEndpointRouter _router;
        private readonly MultiEndpointConnectionContainerFactory _endpointsContainerFactory;

        public NegotiateProcessor(IServiceEndpointManager serviceEndpointManager, IEndpointRouter router, MultiEndpointConnectionContainerFactory endpointsContainerFactory)
        {
            _serviceEndpointManager = serviceEndpointManager;
            _router = router;
            _endpointsContainerFactory = endpointsContainerFactory;
        }

        public async Task<NegotiationResponse> GetClientEndpointAsync(string hubName, HttpContext httpContext = null, string userId = null, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
        {
            try
            {
                if (cancellationToken == default && httpContext != null)
                {
                    cancellationToken = httpContext.RequestAborted;
                }
                var container = _endpointsContainerFactory.GetOrCreate(hubName);
                //ensure connections to each endpoint are initialized, so that the online status of endpoints are valid
                await container.ConnectionInitializedTask.OrTimeout(cancellationToken, Timeout, ConnectionInitializedTaskDescription);

                var candidateEndpoints = _serviceEndpointManager.GetEndpoints(hubName);
                var selectedEndpoint = _router.GetNegotiateEndpoint(httpContext, candidateEndpoints);
                var provider = _serviceEndpointManager.GetEndpointProvider(selectedEndpoint);

                claims ??= httpContext?.User?.Claims;
                var claimsWithUserId = ClaimsUtility.BuildJwtClaims(httpContext?.User, userId: userId, () => claims);

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