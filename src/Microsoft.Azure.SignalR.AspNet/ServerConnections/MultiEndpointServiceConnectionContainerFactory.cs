// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class MultiEndpointServiceConnectionContainerFactory : IServiceConnectionContainerFactory
    {
        private readonly ServiceOptions _options;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceConnectionFactory _serviceConnectionFactory;
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly IEndpointRouter _router;
        private readonly IServerNameProvider _nameProvider;

        public MultiEndpointServiceConnectionContainerFactory(
        IServiceConnectionFactory serviceConnectionFactory,
        IServiceEndpointManager serviceEndpointManager,
        IEndpointRouter router,
        IOptions<ServiceOptions> options,
        IServerNameProvider nameProvider,
        ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _serviceConnectionFactory = serviceConnectionFactory;
            _nameProvider = nameProvider;
            _options = options?.Value;
            _serviceEndpointManager = serviceEndpointManager ?? throw new ArgumentNullException(nameof(serviceEndpointManager));
        }

        public IServiceConnectionContainer Create(string hub)
        {
            return new MultiEndpointServiceConnectionContainer(_serviceConnectionFactory, hub, _options.ConnectionCount, _serviceEndpointManager, _router, _nameProvider, _loggerFactory);
        }
    }
}
