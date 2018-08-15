// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// Maps incoming requests to <see cref="Hub"/> types.
    /// </summary>
    public class ServiceRouteBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RouteBuilder _routes;
        private readonly bool _isDefaultUserIdProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRouteBuilder"/> class.
        /// </summary>
        /// <param name="routes">The routes builder.</param>
        public ServiceRouteBuilder(RouteBuilder routes)
        {
            _routes = routes;
            _serviceProvider = _routes.ServiceProvider;

            var userIdProvider = _serviceProvider.GetService<IUserIdProvider>();
            _isDefaultUserIdProvider = userIdProvider is DefaultUserIdProvider;
        }

        /// <summary>
        /// Maps incoming requests with the specified path to the specified <see cref="Hub"/> type.
        /// </summary>
        /// <typeparam name="THub">The <see cref="Hub"/> type to map requests to.</typeparam>
        /// <param name="path">The request path.</param>
        public void MapHub<THub>(string path) where THub : Hub
            => MapHub<THub>(new PathString(path));

        /// <summary>
        /// Maps incoming requests with the specified path to the specified <see cref="Hub"/> type.
        /// </summary>
        /// <typeparam name="THub">The <see cref="Hub"/> type to map requests to.</typeparam>
        /// <param name="path">The request path.</param>
        public void MapHub<THub>(PathString path) where THub: Hub
        {
            // find auth attributes
            var authorizeAttributes = typeof(THub).GetCustomAttributes<AuthorizeAttribute>(inherit: true);
            var authorizationData = new List<IAuthorizeData>();
            foreach (var attribute in authorizeAttributes)
            {
                authorizationData.Add(attribute);
            }
            _routes.MapRoute(path + "/negotiate", c => RedirectToService(c, typeof(THub).Name, authorizationData));

            Start<THub>();
        }

        private NegotiationResponse GenerateNegotiateResponse(HttpContext context, string hubName)
        {
            var serviceEndpointProvider = _serviceProvider.GetRequiredService<IServiceEndpointProvider>();
            var claims = BuildClaims(context);
            return new NegotiationResponse
            {
                Url = serviceEndpointProvider.GetClientEndpoint(hubName),
                AccessToken = serviceEndpointProvider.GenerateClientAccessToken(hubName, claims),
                // Need to set this even though it's technically protocol violation https://github.com/aspnet/SignalR/issues/2133
                AvailableTransports = new List<AvailableTransport>()
            };
        }

        private IEnumerable<Claim> BuildClaims(HttpContext context)
        {
            var options = _serviceProvider.GetService<IOptions<ServiceOptions>>();
            var claims = options.Value.ClaimsProvider?.Invoke(context) ?? context.User.Claims;

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

        private async Task RedirectToService(HttpContext context, string hubName, List<IAuthorizeData> authorizationData)
        {
            if (!await AuthorizeHelper.AuthorizeAsync(context, authorizationData))
            {
                return;
            }
            context.Response.ContentType = "application/json";
            var negotiateResponse = GenerateNegotiateResponse(context, hubName);
            var writer = new MemoryBufferWriter();

            try
            {
                NegotiateProtocol.WriteResponse(negotiateResponse, writer);
                // Write it out to the response with the right content length
                context.Response.ContentLength = writer.Length;
                await writer.CopyToAsync(context.Response.Body);
            }
            finally
            {
                writer.Reset();
            }
        }

        private void Start<THub>() where THub : Hub
        {
            var app = new ConnectionBuilder(_serviceProvider)
                .UseHub<THub>()
                .Build();

            var dispatcher = _serviceProvider.GetRequiredService<ServiceHubDispatcher<THub>>();
            dispatcher.Start(app);
        }
    }
}
