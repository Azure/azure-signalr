// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Reflection;
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
        private ServiceProvider _serviceProvider;
        private Action<ServiceManagerOptions> _configure;

        /// <summary>
        /// Configures the <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <param name="configure">A callback to configure the <see cref="IServiceManager"/>.</param>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        public ServiceManagerBuilder WithOptions(Action<ServiceManagerOptions> configure)
        {
            _configure = configure;
            return this;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ServiceManagerBuilder WithCallingAssembly()
        {
            _assembly = Assembly.GetCallingAssembly();
            return this;
        }

        /// <summary>
        /// Builds <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IServiceManager"/>.</returns>
        public IServiceManager Build()
        {
            _services.AddSignalRServiceManager(_configure);
            _serviceProvider = _services.BuildServiceProvider();
            var context = _serviceProvider.GetRequiredService<IOptions<ServiceManagerContext>>().Value;
            var productInfo = ProductInfo.GetProductInfo(_assembly);
            var restClientBuilder = new RestClientFactory(productInfo);
            return new ServiceManager(context, restClientBuilder);
        }

        /// <summary>
        /// Dispose unmanaged resources accociated with the builder and the instance built from it.
        /// </summary>
        public void Dispose()
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
        }
    }
}