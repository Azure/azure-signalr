// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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
        /// Adds SignalR Service Manager to the specified services collection.
        /// </summary>
        public static IServiceCollection AddSignalRServiceManager(this IServiceCollection services)
        {
            return services.AddSignalRServiceManager<ServiceManagerOptionsSetup>();
        }

        /// <summary>
        /// Adds SignalR Service Manager to the specified services collection.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="setupInstance">The setup instance. If null, service container will create the instance.</param>
        /// <typeparam name="TOptionsSetup">The type of class used to setup <see cref="ServiceManagerOptions"/>. </typeparam>
        public static IServiceCollection AddSignalRServiceManager<TOptionsSetup>(this IServiceCollection services, TOptionsSetup setupInstance = null) where TOptionsSetup : class, IConfigureOptions<ServiceManagerOptions>, IOptionsChangeTokenSource<ServiceManagerOptions>
        {
            services.SetupOptions<ServiceManagerOptions, TOptionsSetup>(setupInstance);

            return services.AddSignalRServiceCore();
        }

        /// <summary>
        /// Adds SignalR Service Context to the specified services collection.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="setupInstance">The setup instance. If null, service container will create the instance.</param>
        /// <typeparam name="TOptionsSetup">The type of class used to setup <see cref="ServiceManagerOptions"/>. </typeparam>
        public static IServiceCollection AddSignalRServiceContext<TOptionsSetup>(this IServiceCollection services, TOptionsSetup setupInstance = null) where TOptionsSetup : class, IConfigureOptions<ContextOptions>, IOptionsChangeTokenSource<ContextOptions>
        {
            services.SetupOptions<ContextOptions, TOptionsSetup>(setupInstance);
            services.AddSingleton<IServiceContext, ServiceContext>();

            return services.AddSignalRServiceCore();
        }

        private static IServiceCollection AddSignalRServiceCore(this IServiceCollection services)
        {
            services.PostConfigure<ServiceManagerOptions>(o => o.ValidateOptions());
            services.AddSignalR()
                .AddAzureSignalR<CascadeServiceOptionsSetup>();

            //add dependencies for persistent mode only
            services
                .AddSingleton<ConnectionFactory>()
                .AddSingleton<IConnectionFactory, ManagementConnectionFactory>()
                .AddSingleton<ConnectionDelegate>((connectionContext) => Task.CompletedTask)
                .AddSingleton<IServiceConnectionFactory, ServiceConnectionFactory>()
                .AddSingleton<MultiEndpointConnectionContainerFactory>()
                .AddSingleton<IConfigureOptions<HubOptions>, ManagementHubOptionsSetup>();

            services.AddLogging()
                    .AddSingleton<ServiceHubLifetimeManagerFactory>();

            services.AddRestClientFactory();
            services.AddSingleton<NegotiateProcessor>();
            return services.TrySetProductInfo();
        }

        /// <summary>
        /// Adds product info to <see cref="ServiceManagerOptions"/>
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection WithAssembly(this IServiceCollection services, Assembly assembly)
        {
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerOptions>(o => o.ProductInfo = productInfo);
        }

        private static IServiceCollection TrySetProductInfo(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerOptions>(o => o.ProductInfo ??= productInfo);
        }

        private static IServiceCollection AddRestClientFactory(this IServiceCollection services) => services
            .AddHttpClient()
            .AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value;
                var productInfo = options.ProductInfo;
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new RestClientFactory(productInfo, httpClientFactory);
            });
    }
}