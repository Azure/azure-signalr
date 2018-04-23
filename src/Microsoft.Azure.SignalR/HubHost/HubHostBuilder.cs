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
using Microsoft.AspNetCore.Http.Connections.Internal;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    public class HubHostBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RouteBuilder _routes;

        public HubHostBuilder(RouteBuilder routes)
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
            _routes.MapRoute(path + "/negotiate", c => RedirectToServiceUrlWithToken(c, typeof(THub).Name, authorizationData));

            Start<THub>();
        }

        private NegotiationResponse GenServiceUrlAndToken(HttpContext context, string hubName, List<IAuthorizeData> authorizationData)
        {
            var connectionServiceProvider = _serviceProvider.GetRequiredService<IConnectionProvider>();
            var options = _serviceProvider.GetService<IOptions<ServiceOptions>>();
            var claims = options.Value.ClaimsProvider?.Invoke(context) ?? context.User.Claims;
            var negotiationResponse = new NegotiationResponse()
            {
                Url = connectionServiceProvider.GetClientEndpoint(hubName),
                AccessToken = connectionServiceProvider.GenerateClientAccessToken(hubName, claims)
            };
            return negotiationResponse;
        }

        private async Task RedirectToServiceUrlWithToken(HttpContext context, string hubName, List<IAuthorizeData> authorizationData)
        {
            if (!await AuthorizeHelper.AuthorizeAsync(context, authorizationData))
            {
                return;
            }
            context.Response.ContentType = "application/json";
            var serviceProviderResponse = GenServiceUrlAndToken(context, hubName, authorizationData);
            var writer = new MemoryBufferWriter();

            try
            {
                NegotiateProtocol.WriteResponse(serviceProviderResponse, writer);
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
            var hubHost = _serviceProvider.GetRequiredService<HubHost<THub>>();
            hubHost.Configure();
            var connectionBuilder = new ConnectionBuilder(_serviceProvider);
            connectionBuilder.UseHub<THub>();
            var app = connectionBuilder.Build();
            hubHost.StartAsync(app).GetAwaiter().GetResult();
        }
    }
}
