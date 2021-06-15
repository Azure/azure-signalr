// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal static class SerializationDependencyInjectionExtensions
    {
        public static IServiceCollection AddNewtonsoftHubProtocol(this IServiceCollection services, Action<IOptions<NewtonsoftServiceHubProtocolOptions>> configure)
        {
            services.Configure(configure);

            // for persistent transport type only:
#if NETCOREAPP3_0_OR_GREATER
            services.AddSingleton<IHubProtocol, NewtonsoftJsonHubProtocol>();
            services.ConfigureOptions<NewtonsoftHubProtocolOptionsSetup>();
#endif
#if NETSTANDARD2_0
            services.ConfigureOptions<JsonHubProtocolOptionsSetup>();
#endif

            return services;
        }
    }
}