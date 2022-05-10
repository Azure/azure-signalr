// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Extension methods for <see cref="IApplicationBuilder"/>.
    /// </summary>
    public static class AzureSignalRApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds Azure SignalR Service to the <see cref="IApplicationBuilder"/> request execution pipeline.
        /// </summary>
        /// <param name="app">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="configure">A callback to configure the <see cref="ServiceRouteBuilder"/>.</param>
        /// <returns>The same instance of the <see cref="IApplicationBuilder"/> for chaining.</returns>
#if NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
        [Obsolete("Use IApplicationBuilder.UseEndpoints() to replace IApplicationBuilder.UseAzureSignalR when .NET Core version >= 3.1")]
#endif
        public static IApplicationBuilder UseAzureSignalR(this IApplicationBuilder app, Action<ServiceRouteBuilder> configure)
        {
            var marker = app.ApplicationServices.GetService<AzureSignalRMarkerService>();

            // Though in core 3.0 it's not required to call AddAzureSignalR() then UseAzureSignalR()
            // It's not normal behavior to call AddSignalR() with UseAzureSignalR(), so keep the check.
            if (marker == null)
            {
                throw new InvalidOperationException(
                    "Unable to find the required services. Please add all the required services by calling " +
                    "'IServiceCollection.AddAzureSignalR' inside the call to 'ConfigureServices(...)' in the application startup code.");
            }

            marker.IsConfigured = true;
            var routes = new RouteBuilder(app);
            configure(new ServiceRouteBuilder(routes));
            app.UseRouter(routes.Build());
            return app;
        }
    }
}
