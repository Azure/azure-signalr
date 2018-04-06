// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Core;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AzureSignalRDependencyInjectionExtensions
    {
        public static IServiceCollection AddAzureSignalR(this IServiceCollection services,
            Action<HubHostOptions> configure = null)
        {
            if (configure != null) services.Configure(configure);
            
            services.AddSingleton(typeof(HubLifetimeManager<>), typeof(HubHostLifetimeManager<>));
            services.AddSingleton(typeof(IHubProtocolResolver), typeof(DefaultHubProtocolResolver));
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHubProtocol, JsonHubProtocol>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHubProtocol, MessagePackHubProtocol>());
            services.AddSingleton(typeof(IHubContext<>), typeof(HubContext<>));
            services.AddSingleton(typeof(IUserIdProvider), typeof(DefaultUserIdProvider));
            services.AddScoped(typeof(IHubActivator<>), typeof(DefaultHubActivator<>));

            services.AddSingleton(typeof(IConnectionManager), typeof(ConnectionManager));
            services.AddSingleton(typeof(HubHost<>));

            services.AddSingleton(typeof(HubConnectionHandler<>), typeof(HubConnectionHandler<>));
            services.AddSingleton(typeof(HubDispatcher<>), typeof(DefaultHubDispatcher<>));
            services.AddAuthorization();

            return services;
        }
    }
}
