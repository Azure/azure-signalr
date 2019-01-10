// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointManager : ServiceEndpointManagerBase
    {
        private readonly TimeSpan? _ttl;
        private readonly ServiceEndpoint[] _endpoints;
        private readonly ServiceEndpoint[] _primaryEndpoints;

        public ServiceEndpointManager(IOptions<ServiceOptions> options, ILoggerFactory loggerFactory) :
            base(GetEndpoints(options?.Value).ToArray(),
                loggerFactory?.CreateLogger<ServiceEndpointManager>())
        {
            _ttl = options.Value?.AccessTokenLifetime;
        }

        public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
        {
            return new ServiceEndpointProvider(endpoint, _ttl);
        }

        private static IEnumerable<ServiceEndpoint> GetEndpoints(ServiceOptions options)
        {
            // TODO: support multiple endpoints
            var connectionString = options?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(ServiceEndpointProvider.ConnectionStringNotFound);
            }

            yield return new ServiceEndpoint(connectionString);
        }
    }
}
