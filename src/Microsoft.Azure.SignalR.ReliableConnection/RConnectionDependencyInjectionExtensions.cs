// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for <see cref="ISignalRBuilder"/>.
    /// </summary>
    public static class RConnectionDependencyInjectionExtensions
    {
        /// <summary>
        /// Enables the Reload Feature for SignalR.
        /// </summary>
        /// <param name="builder">The <see cref="ISignalRBuilder"/> representing the SignalR server to add Reload Feature.</param>
        /// <returns>The value of <paramref name="builder"/></returns>
        public static TBuilder AddReloadFeature<TBuilder>(this TBuilder builder) where TBuilder : ISignalRBuilder
        {
            builder.Services.Replace(ServiceDescriptor.Singleton<IConnectionFactory, RConnectionFactory>(provider => new RConnectionFactory(new RHttpConnectionFactory(provider, provider.GetService<ILoggerFactory>()))));
            return builder;
        }
    }
}
