// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.SignalR;

namespace Microsoft.AspNetCore.Builder
{
    public static class AzureSignalRApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseAzureSignalR(this IApplicationBuilder app, Action<HubHostBuilder> configure)
        {
            app.UseRoute(routes =>
            {
                configure(new HubHostBuilder(routes));
            });
            return app;
        }

        private static IApplicationBuilder UseRoute(this IApplicationBuilder app, Action<RouteBuilder> callback)
        {
            var routes = new RouteBuilder(app);
            callback(routes);
            app.UseWebSockets();
            app.UseRouter(routes.Build());
            return app;
        }
    }
}
