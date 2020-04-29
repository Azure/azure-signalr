// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class NegotiateHandler
    {
        private readonly IUserIdProvider _userIdProvider;
        private readonly IConnectionRequestIdProvider _connectionRequestIdProvider;
        private readonly Func<HttpContext, IEnumerable<Claim>> _claimsProvider;
        private readonly IServiceEndpointManager _endpointManager;
        private readonly IEndpointRouter _router;
        private readonly string _serverName;
        private readonly ServerStickyMode _mode;
        private readonly bool _enableDetailedErrors;
        private readonly int _endpointsCount;
        private readonly int? _disconnectTimeout;

        public NegotiateHandler(
            IOptions<HubOptions> hubOptions,
            IServiceEndpointManager endpointManager, IEndpointRouter router, IUserIdProvider userIdProvider, IServerNameProvider nameProvider, IConnectionRequestIdProvider connectionRequestIdProvider, IOptions<ServiceOptions> options)
        {
            _endpointManager = endpointManager ?? throw new ArgumentNullException(nameof(endpointManager));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _serverName = nameProvider?.GetName();
            _userIdProvider = userIdProvider ?? throw new ArgumentNullException(nameof(userIdProvider));
            _connectionRequestIdProvider = connectionRequestIdProvider ?? throw new ArgumentNullException(nameof(connectionRequestIdProvider));
            _claimsProvider = options?.Value?.ClaimsProvider;
            _mode = options.Value.ServerStickyMode;
            _enableDetailedErrors = hubOptions.Value.EnableDetailedErrors == true;
            _endpointsCount = options.Value.Endpoints.Length;
            _disconnectTimeout = options.Value.DisconnectTimeoutInSeconds;
        }

        public NegotiationResponse Process(HttpContext context, string hubName)
        {
            var claims = BuildClaims(context);
            var request = context.Request;
            var cultureName = context.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture.Name;
            var originalPath = GetOriginalPath(request.Path);
            var provider = _endpointManager.GetEndpointProvider(_router.GetNegotiateEndpoint(context, _endpointManager.GetEndpoints(hubName)));

            if (provider == null)
            {
                return null;
            }

            var queryString = GetQueryString(request.QueryString.HasValue ? request.QueryString.Value.Substring(1) : null, cultureName);

            return new NegotiationResponse
            {
                Url = provider.GetClientEndpoint(hubName, originalPath, queryString),
                AccessToken = provider.GenerateClientAccessToken(hubName, claims),
                // Need to set this even though it's technically protocol violation https://github.com/aspnet/SignalR/issues/2133
                AvailableTransports = new List<AvailableTransport>()
            };
        }

        private string GetQueryString(string originalQueryString, string cultureName)
        {
            var clientRequestId = _connectionRequestIdProvider.GetRequestId();
            if (clientRequestId != null)
            {
                clientRequestId = WebUtility.UrlEncode(clientRequestId);
            }

            var queryString = $"{Constants.QueryParameter.ConnectionRequestId}={clientRequestId}";
            if (!string.IsNullOrEmpty(cultureName))
            {
                queryString += $"&{Constants.QueryParameter.RequestCulture}={cultureName}";
            }

            return originalQueryString != null 
                ? $"{originalQueryString}&{queryString}"
                : queryString;
        }

        private IEnumerable<Claim> BuildClaims(HttpContext context)
        {
            var userId = _userIdProvider.GetUserId(new ServiceHubConnectionContext(context));
            return ClaimsUtility.BuildJwtClaims(context.User, userId, GetClaimsProvider(context), _serverName, _mode, _enableDetailedErrors, _endpointsCount, _disconnectTimeout).ToList();
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
