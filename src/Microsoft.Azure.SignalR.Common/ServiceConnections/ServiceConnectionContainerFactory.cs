// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

internal class ServiceConnectionContainerFactory : IServiceConnectionContainerFactory
{
    private readonly IServiceEndpointOptions _options;

    private readonly ILoggerFactory _loggerFactory;

    private readonly IServiceEndpointManager _serviceEndpointManager;

    private readonly IMessageRouter _router;

    private readonly IServiceConnectionFactory _serviceConnectionFactory;

    public ServiceConnectionContainerFactory(IServiceConnectionFactory serviceConnectionFactory,
                                             IServiceEndpointManager serviceEndpointManager,
                                             IMessageRouter router,
                                             IServiceEndpointOptions options,
                                             ILoggerFactory loggerFactory)
    {
        _serviceConnectionFactory = serviceConnectionFactory;
        _serviceEndpointManager = serviceEndpointManager ?? throw new ArgumentNullException(nameof(serviceEndpointManager));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public IServiceConnectionContainer Create(string hub, TimeSpan? serviceScaleTimeout = null)
    {
        return new MultiEndpointServiceConnectionContainer(_serviceConnectionFactory,
                                                           hub,
                                                           _options.InitialHubServerConnectionCount,
                                                           _options.MaxHubServerConnectionCount,
                                                           _serviceEndpointManager,
                                                           _router,
                                                           _loggerFactory,
                                                           serviceScaleTimeout);
    }
}
