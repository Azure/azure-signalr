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
    internal static class DependencyInjectionExtensions //TODO: not ready for public use
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
        /// <remarks>Designed for Azure Function extension where the setup of <see cref="ServiceManagerOptions"/> is different from SDK</remarks>
        /// <typeparam name="TOptionsSetup">The type of class used to setup <see cref="ServiceManagerOptions"/>. </typeparam>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection AddSignalRServiceManager<TOptionsSetup>(this IServiceCollection services) where TOptionsSetup : class, IConfigureOptions<ServiceManagerOptions>, IOptionsChangeTokenSource<ServiceManagerOptions>
        {
            //cascade options setup
            services.AddSingleton<TOptionsSetup>()
                    .AddSingleton<IConfigureOptions<ServiceManagerOptions>>(sp => sp.GetService<TOptionsSetup>())
                    .AddSingleton<IOptionsChangeTokenSource<ServiceManagerOptions>>(sp => sp.GetService<TOptionsSetup>());
            services.PostConfigure<ServiceManagerOptions>(o => o.ValidateOptions());
            services.AddSingleton<ServiceManagerContextSetup>()
                    .AddSingleton<IConfigureOptions<ServiceManagerContext>>(sp => sp.GetService<ServiceManagerContextSetup>())
                    .AddSingleton<IOptionsChangeTokenSource<ServiceManagerContext>>(sp => sp.GetService<ServiceManagerContextSetup>());

            services.AddSignalR()
                    .AddAzureSignalR<ServiceOptionsSetup>();

            //add dependencies for persistent mode only
            services
                .AddSingleton<ConnectionFactory>()
                .AddSingleton<IConnectionFactory>(sp =>
                {
                    var productInfo = sp.GetRequiredService<IOptions<ServiceManagerContext>>().Value.ProductInfo;
                    var defaultConnectionFactory = sp.GetRequiredService<ConnectionFactory>();
                    return new ManagementConnectionFactory(productInfo, defaultConnectionFactory);
                })
                .AddSingleton<IServiceConnectionFactory>(sp =>
                    ActivatorUtilities.CreateInstance<ServiceConnectionFactory>(sp, (ConnectionDelegate)((connectionContext) => Task.CompletedTask)))
                .AddSingleton<MultiEndpointConnectionContainerFactory>()
                .AddSingleton<IConfigureOptions<HubOptions>, ManagementHubOptionsSetup>();

            services.AddLogging()
                    .AddSingleton<ServiceHubContextFactory>()
                    .AddSingleton<ServiceHubLifetimeManagerFactory>();
            services.AddSingleton<IServiceManager, ServiceManager>();
            services.AddRestClientFactory();
            return services.TrySetProductInfo();
        }

        /// <summary>
        /// Adds product info to <see cref="ServiceManagerContext"/>
        /// </summary>
        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection WithAssembly(this IServiceCollection services, Assembly assembly)
        {
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerContext>(o => o.ProductInfo = productInfo);
        }

        private static IServiceCollection TrySetProductInfo(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerContext>(o => o.ProductInfo = o.ProductInfo ?? productInfo);
        }

        private static IServiceCollection AddRestClientFactory(this IServiceCollection services) => services
            .AddHttpClient()
            .AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ServiceManagerContext>>().Value;
                var productInfo = options.ProductInfo;
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new RestClientFactory(productInfo, httpClientFactory);
            });
    }
}