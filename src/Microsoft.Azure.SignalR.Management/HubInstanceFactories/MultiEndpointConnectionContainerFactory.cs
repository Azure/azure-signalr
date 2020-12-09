// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiEndpointConnectionContainerFactory
    {
        private readonly ConcurrentDictionary<string, Lazy<MultiEndpointServiceConnectionContainer>> _hubConnectionContainers = new ConcurrentDictionary<string, Lazy<MultiEndpointServiceConnectionContainer>>();
        private readonly IServiceConnectionFactory _connectionFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceEndpointManager _endpointManager;
        private readonly int _connectionCount;
        private readonly IEndpointRouter _router;

        public MultiEndpointConnectionContainerFactory(IServiceConnectionFactory connectionFactory, ILoggerFactory loggerFactory, IServiceEndpointManager serviceEndpointManager, IOptions<ContextOptions> options, IEndpointRouter router = null)
        {
            _connectionFactory = connectionFactory;
            _loggerFactory = loggerFactory;
            _endpointManager = serviceEndpointManager;
            _connectionCount = options.Value.ConnectionCount;
            _router = router;
        }

        public MultiEndpointServiceConnectionContainer GetOrCreate(string hubName, ILoggerFactory loggerFactoryPerHub = null)
        {
            return _hubConnectionContainers.GetOrAdd(hubName, (hubName) => Create(hubName, loggerFactoryPerHub)).Value;
        }

        /// <summary>
        /// creates a lazy container object and starts it after creation.
        /// </summary>
        /// <remarks>
        /// actually the <see cref="MultiEndpointServiceConnectionContainer"/> itself has mechanism to ensure being started only once, using Lazy and starting the container here just to avoid starting it everywhere else
        /// </remarks>
        private Lazy<MultiEndpointServiceConnectionContainer> Create(string hubName, ILoggerFactory loggerFactoryPerHub = null)
        {
            var loggerFactory = loggerFactoryPerHub ?? _loggerFactory;
            return new Lazy<MultiEndpointServiceConnectionContainer>(() =>
            {
                var container = new MultiEndpointServiceConnectionContainer(
                hubName,
                endpoint => new WeakServiceConnectionContainer(_connectionFactory, _connectionCount, endpoint, loggerFactory.CreateLogger<WeakServiceConnectionContainer>()),
                _endpointManager,
                _router,
                loggerFactory);
                container.StartAsync();
                return container;
            },true);
        }
    }
}