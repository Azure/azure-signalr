// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Reflection;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A builder for configuring <see cref="IServiceManager"/> instances.
    /// </summary>
    public class ServiceManagerBuilder : IServiceManagerBuilder
    {
        private readonly ServiceManagerOptions _options = new ServiceManagerOptions();
        private Assembly _assembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// Configures the <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <param name="configure">A callback to configure the <see cref="IServiceManager"/>.</param>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        public ServiceManagerBuilder WithOptions(Action<ServiceManagerOptions> configure)
        {
            configure?.Invoke(_options);
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
            _options.ValidateOptions();
            return new ServiceManager(_options, ProductInfo.GetProductInfo(_assembly));
        }
    }
}