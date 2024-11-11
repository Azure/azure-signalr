// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet;

internal class ServiceEndpointManager : ServiceEndpointManagerBase
{
    private readonly ServiceOptions _options;

    private readonly IAccessKeySynchronizer _synchronizer;

    public ServiceEndpointManager(IAccessKeySynchronizer synchronizer,
                                  ServiceOptions options,
                                  ILoggerFactory loggerFactory) :
        base(options,
             loggerFactory?.CreateLogger<ServiceEndpointManager>())
    {
        _options = options;
        _synchronizer = synchronizer;
    }

    public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
    {
        if (endpoint == null)
        {
            return null;
        }
        _synchronizer.AddServiceEndpoint(endpoint);
        return new ServiceEndpointProvider(endpoint, _options);
    }
}
