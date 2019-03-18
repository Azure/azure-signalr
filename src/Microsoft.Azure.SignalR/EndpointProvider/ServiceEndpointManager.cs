// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointManager : ServiceEndpointManagerBase
    {
        private readonly TimeSpan? _ttl;

        public ServiceEndpointManager(IOptions<ServiceOptions> options, ILoggerFactory loggerFactory) :
            base(options.Value,
                loggerFactory.CreateLogger<ServiceEndpointManager>())
        {
            if (Endpoints.Length == 0)
            {
                throw new ArgumentException(ServiceEndpointProvider.ConnectionStringNotFound);
            }

            _ttl = options.Value?.AccessTokenLifetime;
        }

        public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                return null;
            }

            return new ServiceEndpointProvider(endpoint, _ttl);
        }
    }
}
