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
            var options = new NewtonsoftServiceHubProtocolOptions();
            configure?.Invoke(options);

            // For transient mode.
            services.Configure<ServiceManagerOptions>(o => o.ObjectSerializer = new NewtonsoftJsonObjectSerializer(options.PayloadSerializerSettings));

            // For persistent mode.
            services.AddSingleton<IHubProtocol>(new JsonObjectSerializerHubProtocol(new NewtonsoftJsonObjectSerializer(options.PayloadSerializerSettings)));
            return services;
        }
    }
}