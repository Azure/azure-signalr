// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AzureSignalRDependencyInjectionExtensions
    {
        public static IServiceCollection AddAzureSignalR(this IServiceCollection services,
            Action<HubHostOptions> configure = null)
        {
            if (configure != null) services.Configure(configure);

            services.AddSignalR().AddMessagePackProtocol();

            services.AddSingleton(typeof(HubLifetimeManager<>), typeof(HubHostLifetimeManager<>));
            services.AddSingleton(typeof(IClientConnectionManager), typeof(ClientConnectionManager));
            services.AddSingleton(typeof(IServiceConnectionManager), typeof(ServiceConnectionManager));
            services.AddSingleton(typeof(HubHost<>));
            return services;
        }
    }
}
