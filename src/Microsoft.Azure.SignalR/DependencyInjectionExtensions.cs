// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AzureSignalRDependencyInjectionExtensions
    {
        public static ISignalRServerBuilder AddAzureSignalR(this ISignalRServerBuilder builder,
            Action<ServiceOptions> configure = null)
        {
            if (configure != null) builder.Services.Configure(configure);
            // Assign only once
            if (CloudSignalR.ServiceCollection == null)
            {
                CloudSignalR.ServiceCollection = builder.Services;
            }
            builder.Services.AddSingleton(typeof(HubLifetimeManager<>), typeof(HubHostLifetimeManager<>));
            builder.Services.AddSingleton(typeof(IClientConnectionManager), typeof(ClientConnectionManager));
            builder.Services.AddSingleton(typeof(IServiceConnectionManager), typeof(ServiceConnectionManager));
            builder.Services.AddSingleton(typeof(IConnectionServiceProvider), typeof(ConnectionServiceProvider));
            builder.Services.AddSingleton(typeof(HubHost<>));
            builder.Services.AddSingleton(typeof(IHubMessageSender), typeof(HubMessageSender));
            return builder;
        }
    }
}
