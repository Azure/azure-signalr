// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A builder for configuring <see cref="IServiceManager"/> instances.
    /// </summary>
    public class ServiceManagerBuilder : IServiceManagerBuilder, IDisposable
    {
        private readonly IServiceCollection _services = new ServiceCollection();
        private Assembly _assembly;
        internal ServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// Registers an action used to configure <see cref="IServiceManager"/>.
        /// </summary>
        /// <param name="configure">A callback to configure the <see cref="IServiceManager"/>.</param>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        public ServiceManagerBuilder WithOptions(Action<ServiceManagerOptions> configure)
        {
            _services.Configure(configure);
            return this;
        }

        /// <summary>
        /// Registers a configuration instance to configure <see cref="IServiceManager"/>
        /// </summary>
        /// <param name="config">The configuration instance.</param>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        internal ServiceManagerBuilder WithConfiguration(IConfiguration config)
        {
            _services.AddSingleton(config);
            return this;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ServiceManagerBuilder WithCallingAssembly()
        {
            _assembly = Assembly.GetCallingAssembly();
            _services.WithAssembly(_assembly);
            return this;
        }

        /// <summary>
        /// Builds <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IServiceManager"/>.</returns>
        public IServiceManager Build()
        {
            if (ServiceProvider != null)
            {
                throw new InvalidOperationException($"Mulitple invocation of the method is not allowed.");
            }

            _services.AddSignalRServiceManager();
            ServiceProvider = _services.BuildServiceProvider();
            var context = ServiceProvider.GetRequiredService<IOptions<ServiceManagerContext>>().Value;
            var productInfo = ProductInfo.GetProductInfo(_assembly);
            var restClientBuilder = new RestClientFactory(productInfo);
            return new ServiceManager(context, restClientBuilder);
        }

        /// <summary>
        /// Dispose unmanaged resources accociated with the builder and the instance built from it.
        /// </summary>
        public void Dispose()
        {
            ServiceProvider?.Dispose();
            ServiceProvider = null;
        }
    }
}