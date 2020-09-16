// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Management.MultiEndpoints;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiServiceManagerBuilder : IMultiServiceManagerBuilder  //restricted to internal until ready
    {
        private readonly List<ServiceManagerBuilder> _builders = new List<ServiceManagerBuilder>();
        private readonly List<ServiceEndpoint> _endpoints = new List<ServiceEndpoint>();

        public IEndpointRouter Router { get; set; }

        /// <summary>
        /// Configures the <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <param name="endpoint">Service Endpoint instance.</param>
        /// <param name="configure">A callback to configure the <see cref="IServiceManager"/>. No need to configure the connection string here.</param>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        public MultiServiceManagerBuilder AddEndpointWithOptions(ServiceEndpoint endpoint, Action<ServiceManagerOptions> configure)
        {
            if (endpoint is null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            _builders.Add(new ServiceManagerBuilder().WithOptions(configure).WithOptions(options => options.ConnectionString = endpoint.ConnectionString));
            _endpoints.Add(endpoint);
            return this;
        }

        /// <summary>x
        /// Configures the <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <param name="endpoints">Service Endpoint instances.</param>
        /// <param name="configure">A callback to configure the <see cref="IServiceManager"/>. No need to configure the connection string here.</param>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        public MultiServiceManagerBuilder AddEndpointsWithOptions(IEnumerable<ServiceEndpoint> endpoints, Action<ServiceManagerOptions> configure)
        {
            if (endpoints is null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            foreach (var endpoint in endpoints)
            {
                _builders.Add(new ServiceManagerBuilder().WithOptions(configure).WithOptions(options => options.ConnectionString = endpoint.ConnectionString));
                _endpoints.Add(endpoint);
            }
            return this;
        }

        /// <summary>
        /// Use custom endpoint router.
        /// </summary>
        /// <returns>The same instance of the <see cref="ServiceManagerBuilder"/> for chaining.</returns>
        public MultiServiceManagerBuilder WithRouter(IEndpointRouter router)
        {
            Router = router ?? throw new ArgumentNullException(nameof(router));
            return this;
        }

        public IMultiServiceManager Build()
        {
            Router ??= new DefaultEndpointRouter();
            return new MultiServiceManager(_builders.Select(builder => builder.Build()), _endpoints, Router);
        }
    }
}