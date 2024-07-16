// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal static class IServiceCollectionExtension
    {
        public static IServiceCollection SetupOptions<TOptions, TOptionsSetup>(this IServiceCollection services, TOptionsSetup setupInstance = null) where TOptions : class where TOptionsSetup : class, IConfigureOptions<TOptions>, IOptionsChangeTokenSource<TOptions>
        {
            return (setupInstance == null ? services.AddSingleton<TOptionsSetup>() : services.AddSingleton(setupInstance))
                           .AddSingleton<IConfigureOptions<TOptions>>(sp => sp.GetService<TOptionsSetup>())
                           .AddSingleton<IOptionsChangeTokenSource<TOptions>>(sp => sp.GetService<TOptionsSetup>());
        }

        public static IServiceCollection SetupOptions<TOptions, TOptionsSetup>(this IServiceCollection services, Func<IServiceProvider, TOptionsSetup> implementationFactory) where TOptions : class where TOptionsSetup : class, IConfigureOptions<TOptions>, IOptionsChangeTokenSource<TOptions>
        {
            return  services.AddSingleton(implementationFactory)
                           .AddSingleton<IConfigureOptions<TOptions>>(sp => sp.GetService<TOptionsSetup>())
                           .AddSingleton<IOptionsChangeTokenSource<TOptions>>(sp => sp.GetService<TOptionsSetup>());
        }
    }
}