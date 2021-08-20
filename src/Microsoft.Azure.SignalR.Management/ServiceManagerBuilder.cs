// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A builder for configuring <see cref="IServiceManager"/> instances.
    /// </summary>
    public class ServiceManagerBuilder : IServiceManagerBuilder
    {
        private readonly IServiceCollection _services;
        private Action<IServiceCollection> _configureAction;

        internal ServiceManagerBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public ServiceManagerBuilder() : this(new ServiceCollection())
        {
            _services.AddSignalRServiceManager();
        }

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

        public ServiceManagerBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            _services.AddSingleton(loggerFactory);
            return this;
        }

        public ServiceManagerBuilder WithConfiguration(IConfiguration configuration)
        {
            _services.AddSingleton(configuration);
            return this;
        }

        public ServiceManagerBuilder WithRouter(IEndpointRouter router)
        {
            _services.AddSingleton(router);
            return this;
        }

        /// <summary>
        /// Uses Newtonsoft.Json library to serialize messages sent to SignalR.
        /// </summary>
        /// <param name="configure">A delegate that can be used to configure the <see cref="NewtonsoftServiceHubProtocolOptions"/>.</param>
        /// <returns>The <see cref="ServiceManagerBuilder"/> instance itself.</returns>
        public ServiceManagerBuilder WithNewtonsoftJson(Action<NewtonsoftServiceHubProtocolOptions> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            _services.AddNewtonsoftHubProtocol(configure);
            return this;
        }

        /// <summary>
        /// Uses Newtonsoft.Json library to serialize messages sent to SignalR.
        /// </summary>
        /// <returns>The <see cref="ServiceHubContextBuilder"/> instance itself.</returns>
        public ServiceManagerBuilder WithNewtonsoftJson()
        {
            return WithNewtonsoftJson(o => { });
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public ServiceManagerBuilder WithCallingAssembly()
        {
            var assembly = Assembly.GetCallingAssembly();
            _services.WithAssembly(assembly);
            return this;
        }

        internal ServiceManagerBuilder ConfigureServices(Action<IServiceCollection> configureAction)
        {
            _configureAction = configureAction;
            return this;
        }

        /// <summary>
        /// Builds <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IServiceManager"/>.</returns>
        /// todo: obsolete
        public IServiceManager Build()
        {
            var serviceCollection = new ServiceCollection().Add(_services);
            _configureAction?.Invoke(serviceCollection);
            serviceCollection.AddSingleton(serviceCollection.ToList() as IReadOnlyCollection<ServiceDescriptor>);
            return serviceCollection.BuildServiceProvider()
                .GetRequiredService<IServiceManager>();
        }

        /// <summary>
        /// Builds <see cref="ServiceManager"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="ServiceManager"/>.</returns>
        public ServiceManager BuildServiceManager()
        {
            return Build() as ServiceManager;
        }
    }
}