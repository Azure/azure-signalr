// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal static class DependencyInjectionExtensions //TODO: not ready for public use
    {
        /// <summary>
        /// Add required dependencies according to the configured options <see cref="ServiceManagerOptions"/>. Please call this method after <see cref="ServiceManagerOptions"/> is configured.
        /// </summary>
        public static IServiceCollection AddSignalRServiceManager(this IServiceCollection services)
        {
            services.PostConfigure<ServiceManagerOptions>(o => o.ValidateOptions());
            services.AddSingleton<ServiceManagerContextSetup>()
                .AddSingleton<IConfigureOptions<ServiceManagerContext>>(sp => sp.GetService<ServiceManagerContextSetup>());
            services.AddSingleton<ServiceOptionsSetup>()
                .AddSingleton<IConfigureOptions<ServiceOptions>>(sp => sp.GetService<ServiceOptionsSetup>());
            if (services.Any(descriptor => descriptor.ServiceType == typeof(IOptionsChangeTokenSource<ServiceManagerOptions>)))
            {
                services.AddSingleton<IOptionsChangeTokenSource<ServiceManagerContext>>(sp => sp.GetService<ServiceManagerContextSetup>());
                services.AddSingleton<IOptionsChangeTokenSource<ServiceOptions>>(sp => sp.GetService<ServiceOptionsSetup>());
            }
            services.WithCallingAssembly(overwrite: false);
            return services;
        }

        /// <summary>
        /// Add product info to <see cref="ServiceManagerContext"/>
        /// </summary>
        /// <param name="services"></param>
        /// <param name="overwrite">Overwrites existing 'productInfo' configuration if true. Default is true.</param>
        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection WithCallingAssembly(this IServiceCollection services, bool overwrite = true)
        {
            var assembly = Assembly.GetCallingAssembly();
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerContext>(o =>
            {
                if (overwrite || o.ProductInfo == null)
                {
                    o.ProductInfo = productInfo;
                }
            });
        }
    }
}