// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpointManager : IServiceEndpointManager
    {
        private readonly IServiceEndpointProvider _provider;

        public ServiceEndpointManager(ServiceOptions options)
        {
            var endpoint = new ServiceEndpoint(options.ConnectionString);
            _provider = new ServiceEndpointProvider(endpoint);
        }

        public IServiceEndpointProvider GetEndpointProvider()
        {
            return _provider;
        }
    }
}
