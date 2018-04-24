// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AzureSignalRDependencyInjectionExtensions
    {
        public static ISignalRServerBuilder AddAzureSignalR(this ISignalRServerBuilder builder)
        {
            builder.Services.AddSingleton<IConfigureOptions<ServiceOptions>, ServiceOptionsSetup>();
            return builder.AddAzureSignalRCore();
        }

        public static ISignalRServerBuilder AddAzureSignalR(this ISignalRServerBuilder builder, string connectionString)
        {
            return builder.AddAzureSignalR(options =>
            {
                options.ConnectionString = connectionString;
            });
        }

        public static ISignalRServerBuilder AddAzureSignalR(this ISignalRServerBuilder builder, Action<ServiceOptions> configure)
        {
            builder.Services.Configure(configure);
            return builder.AddAzureSignalRCore();
        }

        public static ISignalRServerBuilder AddAzureSignalRCore(this ISignalRServerBuilder builder)
        {
            builder.Services.AddSingleton(typeof(HubLifetimeManager<>), typeof(ServiceLifetimeManager<>));
            builder.Services.AddSingleton(typeof(IServiceProtocol), typeof(ServiceProtocol));
            builder.Services.AddSingleton(typeof(IClientConnectionManager), typeof(ClientConnectionManager));
            builder.Services.AddSingleton(typeof(IServiceConnectionManager), typeof(ServiceConnectionManager));
            builder.Services.AddSingleton(typeof(IServiceEndpointUtility), typeof(ServiceEndpointUtility));
            builder.Services.AddSingleton(typeof(ServiceHubDispatcher<>));
            builder.Services.AddSingleton(typeof(IHubMessageSender), typeof(HubMessageSender));
            return builder;
        }
    }
}
