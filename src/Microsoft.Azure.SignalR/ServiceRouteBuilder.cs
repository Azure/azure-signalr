// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// Maps incoming requests to <see cref="Hub"/> types.
    /// </summary>
    public class ServiceRouteBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RouteBuilder _routes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRouteBuilder"/> class.
        /// </summary>
        /// <param name="routes">The routes builder.</param>
        public ServiceRouteBuilder(RouteBuilder routes)
        {
            _routes = routes;
            _serviceProvider = _routes.ServiceProvider;
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
        public void MapHub<THub>(PathString path) where THub : Hub
        {
            // Get auth attributes
            var authorizationData = AuthorizeHelper.BuildAuthorizePolicy(typeof(THub));
            _routes.MapRoute(path + Constants.Path.Negotiate, c => ServiceRouteHelper.RedirectToService<THub>(c, authorizationData));

            Start<THub>();
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
