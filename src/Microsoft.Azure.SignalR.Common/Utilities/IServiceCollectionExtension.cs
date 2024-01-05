// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal static class IServiceCollectionExtension
    {
        public static IServiceCollection SetupOptions<TOptions, TOptionsSetup>(this IServiceCollection services) where TOptions : class where TOptionsSetup : class, IConfigureOptions<TOptions>, IOptionsChangeTokenSource<TOptions>
        {
            return services.AddSingleton<TOptionsSetup>()
                           .AddSingleton<IConfigureOptions<TOptions>>(sp => sp.GetService<TOptionsSetup>())
                           .AddSingleton<IOptionsChangeTokenSource<TOptions>>(sp => sp.GetService<TOptionsSetup>());
        }
    }
}