// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
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

        public async Task<NegotiationResponse> NegotiateAsync(string hubName, NegotiationOptions negotiationOptions, CancellationToken cancellationToken = default)
        {
            negotiationOptions ??= NegotiationOptions.Default;
            var httpContext = negotiationOptions.HttpContext;
            var userId = negotiationOptions.UserId;
            var claims = negotiationOptions.Claims;
            var isDiagnosticClient = negotiationOptions.IsDiagnosticClient;
            var enableDetailedErrors = negotiationOptions.EnableDetailedErrors;
            var lifetime = negotiationOptions.TokenLifetime;
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
                var closeOnAuthenticationExpiration = negotiationOptions.CloseOnAuthenticationExpiration;
                var authenticationExpiresOn = closeOnAuthenticationExpiration ? DateTimeOffset.UtcNow.Add(negotiationOptions.TokenLifetime) : default(DateTimeOffset?);
                var claimsWithUserId = ClaimsUtility.BuildJwtClaims(httpContext?.User, userId: userId, claimProvider, enableDetailedErrors: enableDetailedErrors, isDiagnosticClient: isDiagnosticClient, closeOnAuthenticationExpiration: closeOnAuthenticationExpiration, authenticationExpiresOn: authenticationExpiresOn);

                var tokenTask = provider.GenerateClientAccessTokenAsync(hubName, claimsWithUserId, lifetime);
                await tokenTask.OrTimeout(cancellationToken, Timeout, GeneratingTokenTaskDescription);
                return new NegotiationResponse
                {
                    Url = provider.GetClientEndpoint(hubName, null, null),
                    AccessToken = tokenTask.Result
                };
            }
            catch (Exception e) when (e is OperationCanceledException || e is TimeoutException)
            {
                throw new AzureSignalRException(ErrorMsg, e);
            }
        }
    }
}