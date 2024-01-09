// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

#if !NETSTANDARD2_0
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
#endif

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
            return builder.AddAzureSignalR<ServiceOptionsSetup>();
        }

        /// <summary>
        /// Add Azure SignalR with specified connection name, the connection string could be read from ConnectionStrings.{name}, the settings are loaded from Azure:SignalR:{name} section
        /// </summary>
        /// <param name="builder">The <see cref="ISignalRServerBuilder"/>.</param>
        /// <param name="name">The name of the Azure SignalR service that settings and connection strings are read from</param>
        /// <param name="configure">A callback to configure the <see cref="ServiceOptions"/>.</param>
        /// <returns>The same instance of the <see cref="ISignalRServerBuilder"/> for chaining.</returns>
        public static ISignalRServerBuilder AddAzureSignalRWithConnectionName(this ISignalRServerBuilder builder, string name, Action<ServiceOptions> configure = null)
        {
            builder.Services.AddOptions<ServiceOptions>(name)
                .Configure<ServiceOptionsSetup>((o, s) => s.Configure(o));

            builder.Services.SetupOptions<ServiceOptions, ServiceOptionsSetup>(s => ActivatorUtilities.CreateInstance<ServiceOptionsSetup>(s, name));
            if (configure != null)
            {
                builder.Services.Configure(configure).PostConfigure<ServiceOptions>(o => o.Validate());
            }
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
                   .Services.Configure(configure)
                   .PostConfigure<ServiceOptions>(o => o.Validate());

            return builder;
        }

        /// <typeparam name="TOptionsSetup">The set up class used to configure <see cref="ServiceOptions"/> and track changes.</typeparam>
        internal static ISignalRServerBuilder AddAzureSignalR<TOptionsSetup>(this ISignalRServerBuilder builder) where TOptionsSetup : class, IConfigureOptions<ServiceOptions>, IOptionsChangeTokenSource<ServiceOptions>
        {
            builder.Services.SetupOptions<ServiceOptions, TOptionsSetup>();
            return builder.AddAzureSignalRCore();
        }

        private static ISignalRServerBuilder AddAzureSignalRCore(this ISignalRServerBuilder builder)
        {
            builder.Services
                .AddSingleton(typeof(HubLifetimeManager<>), typeof(ServiceLifetimeManager<>))
                .AddSingleton(typeof(IServiceProtocol), typeof(ServiceProtocol))
                .AddSingleton(typeof(IClientConnectionManager), typeof(ClientConnectionManager))
                .AddSingleton(typeof(IServiceConnectionManager<>), typeof(ServiceConnectionManager<>))
                .AddSingleton(typeof(IServiceEndpointManager), typeof(ServiceEndpointManager))
                .AddSingleton(typeof(IServerNameProvider), typeof(DefaultServerNameProvider))
                .AddSingleton(typeof(IBlazorDetector), typeof(DefaultBlazorDetector))
                .AddSingleton(typeof(ServiceHubDispatcher<>))
                .AddSingleton(typeof(ServerLifetimeManager))
                .AddSingleton(typeof(AzureSignalRMarkerService))
                .AddSingleton<IClientConnectionFactory, ClientConnectionFactory>()
                .AddSingleton<IHostedService, HeartBeat>()
                .AddSingleton<IAccessKeySynchronizer, AccessKeySynchronizer>()
                .AddSingleton(typeof(NegotiateHandler<>));

            // If a custom router is added, do not add the default router
            builder.Services.TryAddSingleton(typeof(IEndpointRouter), typeof(DefaultEndpointRouter));
            builder.Services.TryAddSingleton(typeof(IConnectionRequestIdProvider), typeof(DefaultConnectionRequestIdProvider));

            // If a custom service event handler is added, do not add the default handler.
            builder.Services.TryAddSingleton<IServiceEventHandler, DefaultServiceEventHandler>();

            // IEndpointRouter and IAccessKeySynchronizer is required to build ClientInvocationManager.
            builder.Services
#if NET7_0_OR_GREATER
                .AddSingleton<IClientInvocationManager, ClientInvocationManager>();
#else
                .AddSingleton<IClientInvocationManager, DummyClientInvocationManager>();
#endif

#if !NETSTANDARD2_0
            builder.Services.TryAddSingleton<AzureSignalRHostedService>();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupFilter, AzureSignalRStartupFilter>());
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<MatcherPolicy, NegotiateMatcherPolicy>());
#endif

            return builder;
        }
    }
}
