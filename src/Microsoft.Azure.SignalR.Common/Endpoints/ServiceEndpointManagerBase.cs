// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceEndpointManagerBase : IServiceEndpointManager
    {
        private readonly ServiceEndpoint[] _endpoints;
        private readonly ServiceEndpoint[] _primaryEndpoints;
        private readonly ILogger _logger;

        public ServiceEndpointManagerBase(IReadOnlyCollection<ServiceEndpoint> endpoints, ILogger logger)
        {
            if (endpoints.Count == 0)
            {
                throw new AzureSignalRNoEndpointAvailableException();
            }

            _logger = logger ?? NullLogger.Instance;

            var groupedEndpoints = endpoints.GroupBy(s => s.Endpoint).Select(s =>
            {
                var items = s.ToList();
                if (items.Count > 1)
                {
                    // By default pick up the primary endpoint, otherwise the first one
                    var item = items.FirstOrDefault(i => i.EndpointType == EndpointType.Primary) ?? items.FirstOrDefault();
                    Log.DuplicateEndpointFound(_logger, items.Count, item.Endpoint, item.ToString());
                    return item;
                }

                return items[0];
            });

            _endpoints = groupedEndpoints.ToArray();

            _primaryEndpoints = _endpoints.Where(s => s.EndpointType == EndpointType.Primary).ToArray();

            if (_primaryEndpoints.Length == 0)
            {
                throw new AzureSignalRNoPrimaryEndpointException();
            }
        }

        public abstract IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint);

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

        private static class Log
        {
            private static readonly Action<ILogger, int, string, string, Exception> _duplicateEndpointFound =
                LoggerMessage.Define<int, string, string>(LogLevel.Warning, new EventId(1, "DuplicateEndpointFound"), "{count} endpoint to {endpoint} found, use the one {name}");

            public static void DuplicateEndpointFound(ILogger logger, int count, string endpoint, string name)
            {
                _duplicateEndpointFound(logger, count, endpoint, name, null);
            }
        }
    }
}
