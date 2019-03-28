// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Startup
{
#if NETCOREAPP3_0
    using Microsoft.AspNetCore.Http.Endpoints;
#endif
    internal class AzureSignalRStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> build)
        {
            return app =>
            {
#if NETCOREAPP3_0
                build(app);

                // This can't be a hosted service because it needs to run after startup
                var service = app.ApplicationServices.GetRequiredService<AzureSignalRHostedService>();
                service.Start();

                // Redirect negotiate to signalr service
                app.Use(async (context, next) =>
                {
                    var hasHubMetadata = context.GetEndpoint()?.Metadata.GetMetadata<HubMetadata>();

                    if (hasHubMetadata == null || !context.Request.Path.Value.EndsWith(Constants.Path.Negotiate))
                    {
                        await next();
                        return;
                    }

                    // get auth attributes
                    var authorizeAttributes = hasHubMetadata.HubType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);
                    var authorizationData = new List<IAuthorizeData>();
                    foreach (var attribute in authorizeAttributes)
                    {
                        authorizationData.Add((AuthorizeAttribute)attribute);
                    }

                    await ServiceRouteHelper.RedirectToService(context, hasHubMetadata.HubType.Name, authorizationData);
                });
#endif
            };
        }
    }
}
