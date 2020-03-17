﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[assembly: HostingStartup(typeof(AzureSignalRHostingStartup))]

namespace Microsoft.Azure.SignalR
{
    internal class AzureSignalRHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
                if (!context.HostingEnvironment.IsDevelopment() || context.Configuration.GetSection(Constants.Keys.AzureSignalREnabledKey).Get<bool>())
                {
                    services.AddSignalR().AddAzureSignalR();
                }
            });
        }
    }
}
#endif