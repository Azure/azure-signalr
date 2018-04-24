// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
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
    public class ServiceRouteBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RouteBuilder _routes;

        public ServiceRouteBuilder(RouteBuilder routes)
        {
            _routes = routes;
            _serviceProvider = _routes.ServiceProvider;
        }

        public void MapHub<THub>(string path) where THub : Hub
            => MapHub<THub>(new PathString(path));

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
            var serviceEndpointUtility = _serviceProvider.GetRequiredService<IServiceEndpointUtility>();
            var options = _serviceProvider.GetService<IOptions<ServiceOptions>>();
            var claims = options.Value.ClaimsProvider?.Invoke(context) ?? context.User.Claims;
            return new NegotiationResponse
            {
                Url = serviceEndpointUtility.GetClientEndpoint(hubName),
                AccessToken = serviceEndpointUtility.GenerateClientAccessToken(hubName, claims),
                // Need to set this even though it's technically protocol violation https://github.com/aspnet/SignalR/issues/2133
                AvailableTransports = new List<AvailableTransport>()
            };
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
