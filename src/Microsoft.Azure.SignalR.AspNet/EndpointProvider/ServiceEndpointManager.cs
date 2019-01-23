// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpointManager : ServiceEndpointManagerBase
    {
        private readonly TimeSpan? _ttl;

        public ServiceEndpointManager(ServiceOptions options, ILoggerFactory loggerFactory) : 
            base(options,
                loggerFactory?.CreateLogger<ServiceEndpointManager>())
        {
            if (Endpoints.Length == 0)
            {
                throw new ArgumentException(ServiceEndpointProvider.ConnectionStringNotFound);
            }

            _ttl = options.AccessTokenLifetime;
        }

        public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            return new ServiceEndpointProvider(endpoint, _ttl);
        }
    }
}
