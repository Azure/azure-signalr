// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpointManager : ServiceEndpointManagerBase
    {
        private readonly TimeSpan? _ttl;

        public ServiceEndpointManager(ServiceOptions options, ILoggerFactory loggerFactory) : 
            base(GetEndpoints(options).ToArray(),
                loggerFactory?.CreateLogger<ServiceEndpointManager>())
        {
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

        private static IEnumerable<ServiceEndpoint> GetEndpoints(ServiceOptions options)
        {
            // TODO: support multiple endpoints
            var connectionString = options.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(ServiceEndpointProvider.ConnectionStringNotFound);
            }

            yield return new ServiceEndpoint(connectionString);
        }
    }
}
