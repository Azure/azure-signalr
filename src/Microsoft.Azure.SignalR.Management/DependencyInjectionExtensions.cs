// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
        /// <param name="services">The services collection to add services</param>
        /// <param name="setupInstance">The setup instance. If null, service container will create the instance.</param>
        /// <typeparam name="TOptionsSetup">The type of class used to setup <see cref="ServiceManagerOptions"/>. </typeparam>
        public static IServiceCollection AddSignalRServiceManager<TOptionsSetup>(this IServiceCollection services, TOptionsSetup setupInstance = null) where TOptionsSetup : class, IConfigureOptions<ServiceManagerOptions>, IOptionsChangeTokenSource<ServiceManagerOptions>
        {
            services.SetupOptions<ServiceManagerOptions, TOptionsSetup>(setupInstance);

            return services.AddSignalRServiceCore();
        }

        public static IServiceCollection AddHub<THub>(this IServiceCollection services, string hubName)
            where THub : Hub
        {
            //for persistent 
            //use TryAdd to avoid overriding the test implementation added before.
            services.TryAddSingleton<IServiceConnectionContainer>(sp => sp.GetRequiredService<MultiEndpointConnectionContainerFactory>().Create(hubName));
            services.TryAddSingleton<ConnectionService>();
            //for transient
            services.AddSingleton(sp => ActivatorUtilities.CreateInstance<RestHealthCheckService>(sp, hubName));

            return services
                .AddLogging()
                .AddSingleton<ServiceHubLifetimeManagerFactory>()
                .AddSingleton(sp => ActivatorUtilities.CreateInstance<HostedServiceFactory>(sp).Create())
                //The following three lines register three reference types for the same instance.
                .AddSingleton(sp => sp.GetRequiredService<ServiceHubLifetimeManagerFactory>().Create<THub>(hubName))
                .AddSingleton(sp => (HubLifetimeManager<THub>)sp.GetRequiredService<IServiceHubLifetimeManager<THub>>())
                .AddSingleton<IServiceHubLifetimeManager>(sp => sp.GetRequiredService<IServiceHubLifetimeManager<THub>>())
                //used only when THub is Hub
                .AddSingleton<ServiceHubContext>(sp => ActivatorUtilities.CreateInstance<ServiceHubContextImpl>(sp, hubName));
        }

        public static IServiceCollection AddHub<THub, T>(this IServiceCollection services, string hubName)
            where THub : Hub
            where T : class
        {
            return services
                .AddHub<THub>(hubName)
                .AddSingleton<ServiceHubContext<T>>(sp => ActivatorUtilities.CreateInstance<ServiceHubContextImpl<T>>(sp, hubName));
        }

        private static IServiceCollection AddSignalRServiceCore(this IServiceCollection services)
        {
            services.AddSingleton<IServiceManager, ServiceManagerImpl>();
            services.PostConfigure<ServiceManagerOptions>(o => o.ValidateOptions());
            var tempServices = new ServiceCollection()
                .AddSingleton<IEndpointRouter, AutoHealthCheckRouter>()
                .AddSignalR()
                .AddAzureSignalR<CascadeServiceOptionsSetup>().Services
                .Where(service => service.ServiceType != typeof(IServiceConnectionContainer))
                .Where(service => service.ServiceType != typeof(IHostedService));
            services.Add(tempServices);
            // Remove the JsonHubProtocol and add new one.
            // On .NET Standard 2.0, registering multiple hub protocols with the same name is forbidden.
            services.Replace(ServiceDescriptor.Singleton<IHubProtocol>(sp =>
            {
                var objectSerializer = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ObjectSerializer;
                return objectSerializer != null ? new JsonObjectSerializerHubProtocol(objectSerializer) : new JsonHubProtocol();
            }));
            //add dependencies for persistent mode only
            services
                .AddSingleton<ConnectionFactory>()
                .AddSingleton<IConnectionFactory, ManagementConnectionFactory>()
                .AddSingleton<ConnectionDelegate>((connectionContext) => Task.CompletedTask)
                .AddSingleton<IServiceConnectionFactory, ServiceConnectionFactory>()
                .AddSingleton<MultiEndpointConnectionContainerFactory>()
                .AddSingleton<IConfigureOptions<HubOptions>, ManagementHubOptionsSetup>();

            services.AddRestClientFactory();
            services.AddSingleton<NegotiateProcessor>();
            return services.TrySetProductInfo();
        }

        /// <summary>
        /// Adds product info to <see cref="ServiceManagerOptions"/>
        /// </summary>
        public static IServiceCollection WithAssembly(this IServiceCollection services, Assembly assembly)
        {
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerOptions>(o => o.ProductInfo = productInfo);
        }

        /// <summary>
        /// Allows functions extensions to add additional product info.
        /// </summary>
        public static IServiceCollection AddAdditionalProductInfo(this IServiceCollection services, IEnumerable<KeyValuePair<string, string>> additionalProperties)
        {
            foreach (var property in additionalProperties ?? throw new ArgumentNullException(nameof(additionalProperties)))
            {
                if (property.Key == null || property.Value == null)
                {
                    throw new ArgumentException("Properties contain null key or null value");
                }
                foreach (var c in property.Key)
                {
                    if (c == '[' || c == ']' || c == '=')
                    {
                        throw new ArgumentException($"Property key '{property.Key}' contains invalid char '[', ']' or '='");
                    }
                }
                foreach (var c in property.Value)
                {
                    if (c == '[' || c == ']' || c == '=')
                    {
                        throw new ArgumentException($"Property value '{property.Value}' contains invalid char '[', ']' or '='");
                    }
                }
            }
            return services.PostConfigure<ServiceManagerOptions>(o =>
            {
                foreach (var property in additionalProperties)
                {
                    o.ProductInfo += $" [{property.Key}={property.Value}]";
                }
            });
        }

        private static IServiceCollection TrySetProductInfo(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerOptions>(o => o.ProductInfo ??= productInfo);
        }

        private static IServiceCollection AddRestClientFactory(this IServiceCollection services) => services
            .AddHttpClient(Options.DefaultName)
            .ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler() { Proxy = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.Proxy }).Services
            .AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value;
                var productInfo = options.ProductInfo;
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                return new RestClientFactory(productInfo, httpClientFactory);
            });
    }
}