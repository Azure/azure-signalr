// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Azure.Core.Serialization;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Management
{
    internal static class SerializationDependencyInjectionExtensions
    {
        public static IServiceCollection AddNewtonsoftHubProtocol(this IServiceCollection services, Action<NewtonsoftServiceHubProtocolOptions> configure)
        {
            // for persistent transport type only:
            services.Configure(configure);
#if NETCOREAPP3_0_OR_GREATER
            services.AddSingleton<IHubProtocol, NewtonsoftJsonHubProtocol>();
            services.ConfigureOptions<NewtonsoftHubProtocolOptionsSetup>();
#endif
#if NETSTANDARD2_0
            services.ConfigureOptions<JsonHubProtocolOptionsSetup>();
#endif

            // for transient transport type only, will apply to persistent transport type later.
            var options = new NewtonsoftServiceHubProtocolOptions();
            configure?.Invoke(options);
            services.Configure<ServiceManagerOptions>(o => o.ObjectSerializer = new NewtonsoftJsonObjectSerializer(options.PayloadSerializerSettings));

            return services;
        }
    }
}