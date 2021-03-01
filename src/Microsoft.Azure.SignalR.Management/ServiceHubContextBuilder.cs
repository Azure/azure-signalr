// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContextBuilder
    {
        private readonly IServiceCollection _services;

        internal ServiceHubContextBuilder(IServiceCollection services)
        {
            _services = services;
        }

        public ServiceHubContextBuilder() : this(new ServiceCollection())
        {
            _services.AddSignalRServiceManager();
        }

        private Action<IServiceCollection> _configureAction;

        /// <summary>
        /// Registers an action used to configure <see cref="IServiceManager"/>.
        /// </summary>
        /// <param name="configure">A callback to configure the <see cref="IServiceManager"/>.</param>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        public ServiceHubContextBuilder WithOptions(Action<ServiceManagerOptions> configure)
        {
            _services.Configure(configure);
            return this;
        }

        public ServiceHubContextBuilder WithLoggerFactory(ILoggerFactory loggerFactory)
        {
            _services.AddSingleton(loggerFactory);
            return this;
        }

        public ServiceHubContextBuilder WithConfiguration(IConfiguration configuration)
        {
            _services.AddSingleton(configuration);
            return this;
        }

        public ServiceHubContextBuilder WithRouter(IEndpointRouter router)
        {
            _services.AddSingleton(router);
            return this;
        }

        /// <summary>
        /// Provides a hook to configure services before building.
        /// </summary>
        internal ServiceHubContextBuilder ConfigureServices(Action<IServiceCollection> configureAction)
        {
            _configureAction = configureAction;
            return this;
        }

        /// <summary>
        /// Builds <see cref="ServiceHubContext"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IServiceManager"/>.</returns>
        public async Task<ServiceHubContext> CreateAsync(string hubName, CancellationToken cancellationToken)
        {
            //add requried services
            var transportType = _services.BuildServiceProvider()
                .GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ServiceTransportType;
            _services.AddHub(hubName, transportType);
            _configureAction?.Invoke(_services);
            _services.AddSingleton(_services.ToList() as IReadOnlyCollection<ServiceDescriptor>);

            //build
            var serviceHubContext = _services.BuildServiceProvider()
                .GetRequiredService<ServiceHubContextImpl>();

            //initialize
            var connectionContainer = serviceHubContext.ServiceProvider.GetService<IServiceConnectionContainer>();
            if (connectionContainer != null)
            {
                await connectionContainer.ConnectionInitializedTask.OrTimeout(cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await serviceHubContext?.DisposeAsync();
                return null;
            }
            return serviceHubContext.ServiceProvider.GetRequiredService<ServiceHubContextImpl>();
        }
    }
}