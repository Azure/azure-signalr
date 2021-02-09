// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiEndpointConnectionContainerFactory
    {
        private readonly IServiceConnectionFactory _connectionFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceEndpointManager _endpointManager;
        private readonly int _connectionCount;
        private readonly IEndpointRouter _router;
        private readonly bool _needStart;

        public MultiEndpointConnectionContainerFactory(IServiceConnectionFactory connectionFactory, ILoggerFactory loggerFactory, IServiceEndpointManager serviceEndpointManager, IOptions<ServiceManagerOptions> options, IEndpointRouter router = null)
        {
            _connectionFactory = connectionFactory;
            _loggerFactory = loggerFactory;
            _endpointManager = serviceEndpointManager;
            _connectionCount = options.Value.ConnectionCount;
            _router = router;
            _needStart = options.Value.ServiceTransportType == ServiceTransportType.Persistent;
        }

        public MultiEndpointServiceConnectionContainer Connect(string hubName)
        {
            var container = new MultiEndpointServiceConnectionContainer(
                hubName,
                endpoint => new WeakServiceConnectionContainer(_connectionFactory, _connectionCount, endpoint, _loggerFactory.CreateLogger<WeakServiceConnectionContainer>()),
                _endpointManager,
                _router,
                _loggerFactory);
            if (_needStart)
            {
                container.StartAsync();
            }
            return container;
        }
    }
}