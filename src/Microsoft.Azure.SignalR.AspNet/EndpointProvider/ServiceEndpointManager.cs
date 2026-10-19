// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet;

internal class ServiceEndpointManager : ServiceEndpointManagerBase
{
    private readonly ServiceOptions _options;

    private readonly ServiceEndpointAccessKeyStore _accessKeys;

    public ServiceEndpointManager(ServiceOptions options,
                                  ILoggerFactory loggerFactory,
                                  ServiceEndpointAccessKeyStore accessKeys) :
        base(options,
             loggerFactory?.CreateLogger<ServiceEndpointManager>())
    {
        _options = options;
        _accessKeys = accessKeys;
    }

    public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
    {
        if (endpoint == null)
        {
            return null;
        }
        return new ServiceEndpointProvider(endpoint, _options, _accessKeys.GetAccessKey(endpoint));
    }
}
