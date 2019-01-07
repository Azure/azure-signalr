// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceEndpointManager : IServiceEndpointManager
    {
        private readonly TimeSpan? _ttl;
        private readonly ServiceEndpoint[] _endpoints;
        private readonly ServiceEndpoint[] _primaryEndpoints;

        public ServiceEndpointManager(ServiceOptions options)
        {
            _ttl = options.AccessTokenLifetime;

            // TODO: support multiple endpoints
            var connectionString = options.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(ServiceEndpointProvider.ConnectionStringNotFound);
            }

            _endpoints = new ServiceEndpoint[] { new ServiceEndpoint(connectionString) };

            _primaryEndpoints = _endpoints.Where(s => s.EndpointType == EndpointType.Primary).ToArray();

            if (_primaryEndpoints.Length == 0)
            {
                throw new AzureSignalRException("No primary endpoint defined.");
            }
        }

        public IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            return new ServiceEndpointProvider(endpoint, _ttl);
        }

        public IReadOnlyList<ServiceEndpoint> GetAvailableEndpoints()
        {
            return _endpoints;
        }

        /// <summary>
        /// Only primary endpoints will be returned by client /negotiate
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<ServiceEndpoint> GetPrimaryEndpoints()
        {
            return _primaryEndpoints;
        }
    }
}
