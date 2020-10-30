﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Azure.SignalR.Management.Configuration;
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
            services.AddSingleton<IConfigureOptions<ServiceManagerOptions>, ServiceManagerOptionsSetup>();
            services.AddSingleton<ServiceManagerOptionsSetup>()
                .AddSingleton<IConfigureOptions<ServiceManagerOptions>>(sp => sp.GetService<ServiceManagerOptionsSetup>())
                .AddSingleton<IOptionsChangeTokenSource<ServiceManagerOptions>>(sp => sp.GetService<ServiceManagerOptionsSetup>());
            return services.AddSignalRServiceManagerCore();
        }

        /// <summary>
        /// Adds the essential SignalR Service Manager services to the specified services collection and registers an action used to configure <see cref="ServiceManagerOptions"/>
        /// </summary>
        public static IServiceCollection AddSignalRServiceManager(this IServiceCollection services, Action<ServiceManagerOptions> configure)
        {
            services.Configure(configure);
            return services.AddSignalRServiceManagerCore();
        }

        /// <summary>
        /// Adds the minimum essential SignalR Service Manager services to the specified services collection.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        private static IServiceCollection AddSignalRServiceManagerCore(this IServiceCollection services)
        {
            services.PostConfigure<ServiceManagerOptions>(o => o.ValidateOptions());
            services.AddSingleton<ServiceManagerContextSetup>()
                    .AddSingleton<IConfigureOptions<ServiceManagerContext>>(sp => sp.GetService<ServiceManagerContextSetup>())
                    .AddSingleton<IOptionsChangeTokenSource<ServiceManagerContext>>(sp => sp.GetService<ServiceManagerContextSetup>());
            services.AddSingleton<ServiceOptionsSetup>()
                    .AddSingleton<IConfigureOptions<ServiceOptions>>(sp => sp.GetService<ServiceOptionsSetup>())
                    .AddSingleton<IOptionsChangeTokenSource<ServiceOptions>>(sp => sp.GetService<ServiceOptionsSetup>());
            return services.TrySetProductInfo();
        }

        /// <summary>
        /// Adds product info to <see cref="ServiceManagerContext"/>
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IServiceCollection WithCallingAssembly(this IServiceCollection services)
        {
            var assembly = Assembly.GetCallingAssembly();
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerContext>(o =>
            {
                o.ProductInfo = productInfo;
            });
        }

        private static IServiceCollection TrySetProductInfo(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var productInfo = ProductInfo.GetProductInfo(assembly);
            return services.Configure<ServiceManagerContext>(o =>
            {
                if (o.ProductInfo == null)
                {
                    o.ProductInfo = productInfo;
                }
            });
        }
    }
}