// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_0
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Startup
{
    internal class AzureSignalRStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> build)
        {
            return app =>
            {
                build(app);

                // This can't be a hosted service because it needs to run after startup
                var service = app.ApplicationServices.GetRequiredService<AzureSignalRHostedService>();
                service.Start();
            };
        }
    }
}
#endif