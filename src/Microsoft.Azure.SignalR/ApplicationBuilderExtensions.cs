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
            var routes = new RouteBuilder(app);
            configure(new HubHostBuilder(routes));
            app.UseRouter(routes.Build());
            return app;
        }
    }
}
