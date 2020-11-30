// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Please do NOT use following methods on a global <see cref="IServiceCollection"/>,
    /// because <see cref="IServiceManager"/> will dispose the service container when itself get disposed.
    /// </summary>
    internal static class DependencyInjectionExtensions
    {
        /// <summary>
        /// Adds the essential SignalR Service Manager services to the specified services collection and configures <see cref="ServiceManagerOptions"/> with configuration instance registered in service collection.
        /// </summary>
        public static IServiceCollection AddSignalRServiceManager(this IServiceCollection services)
        {
            return services.AddSignalRServiceManager<ServiceManagerOptionsSetup>();
        }

        /// <summary>
        /// Adds the essential SignalR Service Manager services to the specified services collection and registers an action used to configure <see cref="ServiceManagerOptions"/>
        /// </summary>
        public static IServiceCollection AddSignalRServiceManager(this IServiceCollection services, Action<ServiceManagerOptions> configure)
        {
            services.Configure(configure);
            return services.AddSignalRServiceManager();
        }

        /// <summary>
        /// Adds the essential SignalR Service Manager services to the specified services collection.
        /// </summary>
        /// <remarks>Designed for Azure Function extension</remarks>
        /// <param name="services"></param>
        /// <param name="setupInstance">The setup instance. </param>
        /// <typeparam name="TOptionsSetup">The type of class used to setup <see cref="ServiceManagerOptions"/>. </typeparam>
        public static IServiceCollection AddSignalRServiceManager<TOptionsSetup>(this IServiceCollection services, TOptionsSetup setupInstance = null) where TOptionsSetup : class, IConfigureOptions<ServiceManagerOptions>, IOptionsChangeTokenSource<ServiceManagerOptions>
        {
            //cascade options setup
            services.SetupOptions<ServiceManagerOptions, TOptionsSetup>(setupInstance);
            services.PostConfigure<ContextOptions>(o => o.ValidateOptions());
            services.SetupOptions<ContextOptions,ContextOptionsSetup>();

            services.AddSignalR()
                    .AddAzureSignalR<ServiceOptionsSetup>();

            //add dependencies for persistent mode only
            services
                .AddSingleton<ConnectionFactory>()
                .AddSingleton<IConnectionFactory,ManagementConnectionFactory>()
                .AddSingleton<ConnectionDelegate>((connectionContext) => Task.CompletedTask)
                .AddSingleton<IServiceConnectionFactory, ServiceConnectionFactory>()
                .AddSingleton<MultiEndpointConnectionContainerFactory>()
                .AddSingleton<IConfigureOptions<HubOptions>, ManagementHubOptionsSetup>();

            services.AddLogging()
                    .AddSingleton<ServiceHubContextFactory>()
                    .AddSingleton<ServiceHubLifetimeManagerFactory>();

            //obsolete
            services.AddSingleton<IServiceManager, ServiceManager>();

            services.AddSingleton<IServiceContext, ServiceContext>();
            services.AddRestClientFactory();
            services.AddSingleton<NegotiateProcessor>();
            return services.TrySetProductInfo();
        }

        /// <summary>
        /// Adds product info to <see cref="ContextOptions"/>
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection WithAssembly(this IServiceCollection services, Assembly assembly)
        {
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ContextOptions>(o => o.ProductInfo = productInfo);
        }

        private static IServiceCollection TrySetProductInfo(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ContextOptions>(o => o.ProductInfo ??= productInfo);
        }

        private static IServiceCollection AddRestClientFactory(this IServiceCollection services) => services
            .AddHttpClient()
            .AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ContextOptions>>().Value;
                var productInfo = options.ProductInfo;
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new RestClientFactory(productInfo, httpClientFactory);
            });
    }
}