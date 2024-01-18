// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
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
            services.SetupOptions<ServiceManagerOptions, ServiceManagerOptionsSetup>();

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
                var serviceManagerOptions = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value;
                var objectSerializer = serviceManagerOptions.ObjectSerializer;
                if (objectSerializer != null)
                {
                    return new JsonObjectSerializerHubProtocol(objectSerializer);
                }
#pragma warning disable CS0618 // Type or member is obsolete
                // The default protocol is different for historical reason.
                return serviceManagerOptions.ServiceTransportType == ServiceTransportType.Transient ?
                    new JsonObjectSerializerHubProtocol(new NewtonsoftJsonObjectSerializer(serviceManagerOptions.JsonSerializerSettings))
                    :
                    new JsonHubProtocol();
#pragma warning restore CS0618 // Type or member is obsolete
            }));
            //add dependencies for persistent mode only
            services
                .AddSingleton<ConnectionFactory>()
                .AddSingleton<IConnectionFactory, ManagementConnectionFactory>()
                .AddSingleton<ConnectionDelegate>((connectionContext) => Task.CompletedTask)
                .AddSingleton<IServiceConnectionFactory, ServiceConnectionFactory>()
                .AddSingleton<MultiEndpointConnectionContainerFactory>()
                .AddSingleton<IConfigureOptions<HubOptions>, ManagementHubOptionsSetup>();

            //add dependencies for transient mode only
            services.AddSingleton<PayloadBuilderResolver>();

            services.AddRestClient();
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
        public static IServiceCollection AddUserAgent(this IServiceCollection services, string userAgent)
        {
            if (userAgent is null)
            {
                throw new ArgumentNullException(nameof(userAgent));
            }

            return services.PostConfigure<ServiceManagerOptions>(o =>
            {
                if (o.ProductInfo == null)
                {
                    throw new InvalidOperationException("Product info is null");
                }
                o.ProductInfo += userAgent;
            });
        }

        private static IServiceCollection TrySetProductInfo(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerOptions>(o => o.ProductInfo ??= productInfo);
        }

        private static IServiceCollection AddRestClient(this IServiceCollection services)
        {
            // For internal health check. Not impacted by user set timeout.
            services
                .AddHttpClient(Constants.HttpClientNames.InternalDefault, ConfigureProduceInfo)
                .ConfigurePrimaryHttpMessageHandler(ConfigureProxy);

            // Used by user. Impacted by user set timeout.
            services.AddSingleton(sp => sp.GetRequiredService<PayloadBuilderResolver>().GetPayloadContentBuilder())
                    .AddSingleton<RestClient>()
                    .AddSingleton<IBackOffPolicy>(sp =>
                    {
                        var options = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value;
                        var retryOptions = options.RetryOptions;
                        return retryOptions == null
                            ? new DummyBackOffPolicy()
                            : retryOptions.Mode switch
                            {
                                ServiceManagerRetryMode.Fixed => ActivatorUtilities.CreateInstance<FixedBackOffPolicy>(sp),
                                ServiceManagerRetryMode.Exponential => ActivatorUtilities.CreateInstance<ExponentialBackOffPolicy>(sp),
                                _ => throw new NotSupportedException($"Retry mode {retryOptions.Mode} is not supported.")
                            };
                    });
            services
                .AddHttpClient(Constants.HttpClientNames.UserDefault, (sp, client) =>
                {
                    ConfigureUserTimeout(sp, client);
                    ConfigureProduceInfo(sp, client);
                })
                .ConfigurePrimaryHttpMessageHandler(ConfigureProxy);

            services
                .AddHttpClient(Constants.HttpClientNames.Resilient, (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value;
                    if (options.RetryOptions == null)
                    {
                        client.Timeout = options.HttpClientTimeout;
                    }
                    else
                    {
                        // The timeout is enforced by TimeoutHttpMessageHandler.
                        client.Timeout = Timeout.InfiniteTimeSpan;
                    }
                    ConfigureProduceInfo(sp, client);
                    ConfigureMessageTracingId(sp, client);
                })
                .ConfigurePrimaryHttpMessageHandler(ConfigureProxy)
                .AddHttpMessageHandler(sp => ActivatorUtilities.CreateInstance<RetryHttpMessageHandler>(sp, (HttpStatusCode code) => IsTransientErrorForNonMessageApi(code)))
                .AddHttpMessageHandler(sp => ActivatorUtilities.CreateInstance<TimeoutHttpMessageHandler>(sp));

            services
                .AddHttpClient(Constants.HttpClientNames.MessageResilient, (sp, client) =>
                {
                    ConfigureUserTimeout(sp, client);
                    ConfigureProduceInfo(sp, client);
                    ConfigureMessageTracingId(sp, client);
                })
                .ConfigurePrimaryHttpMessageHandler(ConfigureProxy)
                .AddHttpMessageHandler(sp => ActivatorUtilities.CreateInstance<RetryHttpMessageHandler>(sp, (HttpStatusCode code) => IsTransientErrorAndIdempotentForMessageApi(code)));

            return services;

            static HttpMessageHandler ConfigureProxy(IServiceProvider sp) => new HttpClientHandler() { Proxy = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.Proxy };

            static bool IsTransientErrorAndIdempotentForMessageApi(HttpStatusCode code) =>
                // Runtime returns 500 for timeout errors too, to avoid duplicate message, we exclude 500 here.
                code > HttpStatusCode.InternalServerError;

            static bool IsTransientErrorForNonMessageApi(HttpStatusCode code) =>
                code >= HttpStatusCode.InternalServerError ||
                code == HttpStatusCode.RequestTimeout;

            static void ConfigureUserTimeout(IServiceProvider sp, HttpClient client) => client.Timeout = sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.HttpClientTimeout;

            static void ConfigureProduceInfo(IServiceProvider sp, HttpClient client) =>
                client.DefaultRequestHeaders.Add(Constants.AsrsUserAgent, sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ProductInfo ??
                    // The following value should not be used.
                    "Microsoft.Azure.SignalR.Management/");

            static void ConfigureMessageTracingId(IServiceProvider sp, HttpClient client)
            {
                if (sp.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.EnableMessageTracing)
                {
                    client.DefaultRequestHeaders.Add(Constants.Headers.AsrsMessageTracingId, MessageWithTracingIdHelper.Generate().ToString());
                }
            }
        }
    }
}
