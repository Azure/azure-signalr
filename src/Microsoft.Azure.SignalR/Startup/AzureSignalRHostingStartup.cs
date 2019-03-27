﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: HostingStartup(typeof(AzureSignalRHostingStartup))]

namespace Microsoft.Azure.SignalR.Startup
{
    internal class AzureSignalRHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
#if NETCOREAPP3_0
            builder.ConfigureServices((context, services) =>
            {
                if (!context.HostingEnvironment.IsDevelopment() || context.Configuration.GetSection("Azure:SignalR:Enabled").Get<bool>())
                {
                    services.AddSignalR().AddAzureSignalR();
                }
            });
#endif
        }
    }
}
