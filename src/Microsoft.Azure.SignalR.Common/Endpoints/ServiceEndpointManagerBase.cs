// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceEndpointManagerBase : IServiceEndpointManager
    {
        // endpoints ready for client negotiation
        private readonly ConcurrentDictionary<string, IReadOnlyList<HubServiceEndpoint>> _endpointsPerHub = new ConcurrentDictionary<string, IReadOnlyList<HubServiceEndpoint>>();

        private readonly ILogger _logger;

        public ServiceEndpoint[] Endpoints { get; }

        protected ServiceEndpointManagerBase(IServiceEndpointOptions options, ILogger logger) 
            : this(GetEndpoints(options), logger)
        {
        }

        // for test purpose
        internal ServiceEndpointManagerBase(IEnumerable<ServiceEndpoint> endpoints, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // select the most valuable endpoint with the same endpoint address
            var groupedEndpoints = endpoints.Distinct().GroupBy(s => s.Endpoint).Select(s =>
            {
                var items = s.ToList();
                if (items.Count == 1)
                {
                    return items[0];
                }

                // By default pick up the primary endpoint, otherwise the first one
                var item = items.FirstOrDefault(i => i.EndpointType == EndpointType.Primary) ?? items.FirstOrDefault();
                Log.DuplicateEndpointFound(_logger, items.Count, item?.Endpoint, item?.ToString());
                return item;
            });

            Endpoints = groupedEndpoints.ToArray();

            if (Endpoints.Length > 0 && Endpoints.All(s => s.EndpointType != EndpointType.Primary))
            {
                // Only throws when endpoint count > 0
                throw new AzureSignalRNoPrimaryEndpointException();
            }
        }

        public abstract IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint);

        public IReadOnlyList<HubServiceEndpoint> GetEndpoints(string hub)
        {
            return _endpointsPerHub.GetOrAdd(hub, s => Endpoints.Select(e =>
            {
                var provider = GetEndpointProvider(e);
                return new HubServiceEndpoint(hub, provider, e);
            }).ToArray());
        }

        public void AddServiceEndpointToNegotiation(string hub, ServiceEndpoint endpoint)
        {
            if (_endpointsPerHub.TryGetValue(hub, out var hubEndpoints))
            {
                var provider = GetEndpointProvider(endpoint);
                var hubServiceEndpoint = new HubServiceEndpoint(hub, provider, endpoint);
                var updatedHubEndpoints = hubEndpoints.Append(hubServiceEndpoint).ToArray();
                _endpointsPerHub.TryUpdate(hub, updatedHubEndpoints, hubEndpoints);
                return;
            }
            Log.ServiceEndpointAlreadyExist(_logger, endpoint.Endpoint);
        }

        public void RemoveServiceEndpointFromNegotiation(string hub, ServiceEndpoint endpoint)
        {
            if (_endpointsPerHub.TryGetValue(hub, out var hubEndpoints))
            {
                // Keep those except target endpoint
                var updatedHubEndpoints = hubEndpoints.Where(e => e.ConnectionString != endpoint.ConnectionString).ToArray();
                if (!_endpointsPerHub.TryUpdate(hub, updatedHubEndpoints, hubEndpoints))
                {
                    Log.FailedToRemoveEndpointFromNegotiationRouter(_logger, endpoint.Endpoint);
                    
                }
                return;
            }
            Log.ServiceEndpointNotExist(_logger, endpoint.Endpoint);
        }

        public HubServiceEndpoint GenerateHubServiceEndpoint(string hub, ServiceEndpoint endpoint)
        {
            var provider = GetEndpointProvider(endpoint);
            return new HubServiceEndpoint(hub, provider, endpoint);
        }

        private static IEnumerable<ServiceEndpoint> GetEndpoints(IServiceEndpointOptions options)
        {
            if (options == null)
            {
                yield break;
            }

            var endpoints = options.Endpoints;
            var connectionString = options.ConnectionString;

            if (!string.IsNullOrEmpty(connectionString))
            {
                yield return new ServiceEndpoint(options.ConnectionString);
            }

            // ConnectionString can be set by custom Configure
            // Return both the one from ConnectionString and from Endpoints
            if (endpoints != null)
            {
                foreach (var endpoint in endpoints)
                {
                    yield return endpoint;
                }
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, int, string, string, Exception> _duplicateEndpointFound =
                LoggerMessage.Define<int, string, string>(LogLevel.Warning, new EventId(1, "DuplicateEndpointFound"), "{count} endpoint configurations to '{endpoint}' found, use '{name}'.");

            private static readonly Action<ILogger, string, Exception> _serviceEndpointAlreadyExist =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "ServiceEndpointAlreadyExists"), "Add Endpoint {endpoint} already exists, skip add to negotiation router.");

            private static readonly Action<ILogger, string, Exception> _serviceEndpointNotExist =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "ServiceEndpointNotExists"), "Remove endpoint {endpoint} not exist, skip remove from negotiation router.");

            private static readonly Action<ILogger, string, Exception> _failedToRemoveEndpointFromNegotiationRouter =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "ServiceEndpointNotExists"), "Failed to remove endpoint {endpoint} from negotiation router.");


            public static void DuplicateEndpointFound(ILogger logger, int count, string endpoint, string name)
            {
                _duplicateEndpointFound(logger, count, endpoint, name, null);
            }

            public static void ServiceEndpointAlreadyExist(ILogger logger, string endpoint)
            {
                _serviceEndpointAlreadyExist(logger, endpoint, null);
            }

            public static void ServiceEndpointNotExist(ILogger logger, string endpoint)
            {
                _serviceEndpointNotExist(logger, endpoint, null);
            }

            public static void FailedToRemoveEndpointFromNegotiationRouter(ILogger logger, string endpoint)
            {
                _failedToRemoveEndpointFromNegotiationRouter(logger, endpoint, null);
            }
        }
    }
}
