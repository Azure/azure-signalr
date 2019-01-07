// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointManager : IServiceEndpointManager
    {
        private readonly IServiceEndpointProvider _provider;

        public ServiceEndpointManager(IOptions<ServiceOptions> options)
        {
            var endpoint = new ServiceEndpoint(options.Value.ConnectionString);
            _provider = new ServiceEndpointProvider(endpoint, options.Value.AccessTokenLifetime);
        }

        public IServiceEndpointProvider GetEndpointProvider()
        {
            return _provider;
        }
    }
}
