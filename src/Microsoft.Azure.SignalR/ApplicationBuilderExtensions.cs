// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR;

namespace Microsoft.AspNetCore.Builder
{
    public static class AzureSignalRApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseAzureSignalR(this IApplicationBuilder app,
            string connectionString, Action<HubHostBuilder> configure)
        {
            // Assign only once
            CloudSignalR.ServiceProvider = app.ApplicationServices;

            var builder = new HubHostBuilder(app.ApplicationServices,
                CloudSignalR.CreateEndpointProviderFromConnectionString(connectionString),
                CloudSignalR.CreateTokenProviderFromConnectionString(connectionString));
            configure(builder);

            return app;
        }
    }
}
