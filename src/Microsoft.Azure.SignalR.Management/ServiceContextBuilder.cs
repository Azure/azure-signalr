// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceContextBuilder
    {
        private readonly IServiceCollection _services = new ServiceCollection();

        /// <summary>
        /// Registers an action used to configure <see cref="IServiceContext"/>.
        /// </summary>
        /// <param name="configure">A callback to configure the <see cref="IServiceContext"/>.</param>
        /// <returns>The same instance of the <see cref="ServiceContextBuilder"/> for chaining.</returns>
        public ServiceContextBuilder WithOptions(Action<ServiceManagerOptions> configure)
        {
            _services.Configure(configure);
            return this;
        }

        public ServiceContextBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            _services.AddSingleton(loggerFactory);
            return this;
        }

        public ServiceContextBuilder WithConfiguration(IConfiguration configuration)
        {
            _services.AddSingleton(configuration);
            return this;
        }

        public ServiceContextBuilder WithRouter(IEndpointRouter router)
        {
            _services.AddSingleton(router);
            return this;
        }

        internal ServiceContextBuilder WithCallingAssembly()
        {
            var assembly = Assembly.GetCallingAssembly();
            _services.WithAssembly(assembly);
            return this;
        }

        /// <summary>
        /// Builds <see cref="IServiceContext"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IServiceContext"/>.</returns>
        public IServiceContext Build()
        {
            _services.AddSignalRServiceManager();
            var serviceProvider = _services.BuildServiceProvider();
            return serviceProvider.GetRequiredService<IServiceContext>();
        }
    }
}