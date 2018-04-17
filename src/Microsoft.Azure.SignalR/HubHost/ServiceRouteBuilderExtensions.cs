// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Azure.SignalR
{
    public static class ServiceRouteBuilderExtensions
    {
        public static IApplicationBuilder UseRoute(this IApplicationBuilder app, Action<RouteBuilder> callback)
        {
            var routes = new RouteBuilder(app);
            callback(routes);
            app.UseWebSockets();
            app.UseRouter(routes.Build());
            return app;
        }
    }
}
