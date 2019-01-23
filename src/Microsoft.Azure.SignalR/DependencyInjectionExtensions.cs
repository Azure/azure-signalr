// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for <see cref="ISignalRServerBuilder"/>.
    /// </summary>
    public static class AzureSignalRDependencyInjectionExtensions
    {
        /// <summary>
        /// Adds the minimum essential Azure SignalR services to the specified <see cref="ISignalRServerBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="ISignalRServerBuilder"/>.</param>
        /// <returns>The same instance of the <see cref="ISignalRServerBuilder"/> for chaining.</returns>
        /// <remarks>
        /// It reads connection string from a configuration entry Azure:SignalR:ConnectionString.
        /// In development environment, try `dotnet user-secrets set Azure:SignalR:ConnectionString {YourConnectionString}`.
        /// </remarks>
        public static ISignalRServerBuilder AddAzureSignalR(this ISignalRServerBuilder builder)
        {
            builder.Services.AddSingleton<IConfigureOptions<ServiceOptions>, ServiceOptionsSetup>();
            return builder.AddAzureSignalRCore();
        }

        /// <summary>
        /// Adds the minimum essential Azure SignalR services to the specified <see cref="ISignalRServerBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="ISignalRServerBuilder"/>.</param>
        /// <param name="connectionString">The connection string of an Azure SignalR Service instance.</param>
        /// <returns>The same instance of the <see cref="ISignalRServerBuilder"/> for chaining.</returns>
        public static ISignalRServerBuilder AddAzureSignalR(this ISignalRServerBuilder builder, string connectionString)
        {
            return builder.AddAzureSignalR(options =>
            {
                options.ConnectionString = connectionString;
            });
        }

        /// <summary>
        /// Adds the minimum essential Azure SignalR services to the specified <see cref="ISignalRServerBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="ISignalRServerBuilder"/>.</param>
        /// <param name="configure">A callback to configure the <see cref="ServiceOptions"/>.</param>
        /// <returns>The same instance of the <see cref="ISignalRServerBuilder"/> for chaining.</returns>
        public static ISignalRServerBuilder AddAzureSignalR(this ISignalRServerBuilder builder, Action<ServiceOptions> configure)
        {
            builder.AddAzureSignalR()
                   .Services.Configure(configure);

            return builder;
        }

        private static ISignalRServerBuilder AddAzureSignalRCore(this ISignalRServerBuilder builder)
        {
            builder.Services
                .AddSingleton(typeof(HubLifetimeManager<>), typeof(ServiceLifetimeManager<>))
                .AddSingleton(typeof(IServiceProtocol), typeof(ServiceProtocol))
                .AddSingleton(typeof(IClientConnectionManager), typeof(ClientConnectionManager))
                .AddSingleton(typeof(IServiceConnectionManager<>), typeof(ServiceConnectionManager<>))
                .AddSingleton(typeof(IServiceEndpointManager), typeof(ServiceEndpointManager))
                .AddSingleton(typeof(ServiceHubDispatcher<>))
                .AddSingleton(typeof(AzureSignalRMarkerService))
                .AddSingleton<IClientConnectionFactory, ClientConnectionFactory>()
                .AddSingleton<IHostedService, HeartBeat>()
                .AddSingleton<NegotiateHandler>();

            // If a custom router is added, do not add the default router
            builder.Services.TryAddSingleton(typeof(IEndpointRouter), typeof(DefaultEndpointRouter));
            return builder;
        }
    }
}
