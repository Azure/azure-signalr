// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceEndpointManagerBase : IServiceEndpointManager
    {
        private readonly ConcurrentDictionary<string, IReadOnlyList<HubServiceEndpoint>> _endpointsPerHub = new ConcurrentDictionary<string, IReadOnlyList<HubServiceEndpoint>>();

        private readonly ILogger _logger;

        public ServiceEndpoint[] Endpoints { get; }

        public event EndpointEventHandler OnAdd;
        public event EndpointEventHandler OnRemove;
        public event EndpointEventHandler OnUpdate;

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
            return _endpointsPerHub.GetOrAdd(hub, s => Endpoints.Select(e => CreateHubServiceEndpoint(hub, e)).ToArray());
        }

        protected async Task AddServiceEndpointsAsync(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            if (endpoints.Count > 0)
            {
                var hubEndpoints = CreateHubServiceEndpoints(endpoints);

                await Task.WhenAll(hubEndpoints.Select(e => AddServiceEndpoint(e)));

                UpdateNegotiationEndpointsStore(hubEndpoints, ScaleOperation.Add);
            }
        }

        protected Task RemoveServiceEndpoints(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            if (endpoints.Count > 0)
            {
                var hubEndpoints = CreateHubServiceEndpoints(endpoints);

                UpdateNegotiationEndpointsStore(hubEndpoints, ScaleOperation.Remove);

                return Task.WhenAll(hubEndpoints.Select(e => RemoveServiceEndpoint(e)));
            }
            return Task.CompletedTask;
        }

        protected Task RenameSerivceEndpoints(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            if (endpoints.Count > 0)
            {
                var hubEndpoints = CreateHubServiceEndpoints(endpoints);

                UpdateNegotiationEndpointsStore(hubEndpoints, ScaleOperation.Rename);

                return Task.WhenAll(hubEndpoints.Select(e => UpdateServiceEndpoint(e)));
            }
            return Task.CompletedTask;
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

        private IReadOnlyList<HubServiceEndpoint> CreateHubServiceEndpoints(string hub, IEnumerable<ServiceEndpoint> endpoints)
        {
            return endpoints.Select(e => CreateHubServiceEndpoint(hub, e)).ToList();
        }

        private HubServiceEndpoint CreateHubServiceEndpoint(string hub, ServiceEndpoint endpoint)
        {
            var provider = GetEndpointProvider(endpoint);
            return new HubServiceEndpoint(hub, provider, endpoint);
        }

        private void UpdateNegotiationEndpointsStore(IReadOnlyList<HubServiceEndpoint> endpoints, ScaleOperation scaleOperation)
        {
            foreach (var hubEndpoint in _endpointsPerHub)
            {
                var deltaEndpoints = endpoints.Where(e => e.Hub == hubEndpoint.Key).ToList();
                var updatedEndpoints = hubEndpoint.Value.ToList();
                switch (scaleOperation)
                {
                    case ScaleOperation.Add:
                        updatedEndpoints.AddRange(deltaEndpoints);
                        break;
                    case ScaleOperation.Remove:
                        updatedEndpoints = updatedEndpoints.Except(deltaEndpoints, new HubServiceEndpointWeakComparer()).ToList();
                        break;
                    case ScaleOperation.Rename:
                        updatedEndpoints = updatedEndpoints.Except(deltaEndpoints, new HubServiceEndpointWeakComparer()).ToList();
                        updatedEndpoints.AddRange(deltaEndpoints);
                        break;
                }
                _endpointsPerHub.TryUpdate(hubEndpoint.Key, updatedEndpoints, hubEndpoint.Value);
            }
        }

        private IReadOnlyList<HubServiceEndpoint> CreateHubServiceEndpoints(IEnumerable<ServiceEndpoint> endpoints)
        {
            var hubEndpoints = new List<HubServiceEndpoint>();
            var hubs = _endpointsPerHub.Keys;
            foreach (var hub in hubs)
            {
                hubEndpoints.AddRange(CreateHubServiceEndpoints(hub, endpoints));
            }
            return hubEndpoints;
        }

        private Task AddServiceEndpoint(HubServiceEndpoint endpoint)
        {
            OnAdd?.Invoke(endpoint);
            // Wait for new endpoint turn Ready
            while (!endpoint.Ready);

            return Task.CompletedTask;
        }

        private Task RemoveServiceEndpoint(HubServiceEndpoint endpoint)
        {
            OnRemove?.Invoke(endpoint);
            return Task.CompletedTask;
        }

        private Task UpdateServiceEndpoint(HubServiceEndpoint endpoint)
        {
            OnUpdate?.Invoke(endpoint);
            return Task.CompletedTask;
        }

        private sealed class HubServiceEndpointWeakComparer : IEqualityComparer<HubServiceEndpoint>
        {
            public bool Equals(HubServiceEndpoint x, HubServiceEndpoint y)
            {
                return x.ConnectionString == y.ConnectionString && x.EndpointType == y.EndpointType;
            }
        
            public int GetHashCode(HubServiceEndpoint obj)
            {
                return obj.ConnectionString.GetHashCode() ^ obj.EndpointType.GetHashCode();
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, int, string, string, Exception> _duplicateEndpointFound =
                LoggerMessage.Define<int, string, string>(LogLevel.Warning, new EventId(1, "DuplicateEndpointFound"), "{count} endpoint configurations to '{endpoint}' found, use '{name}'.");

            public static void DuplicateEndpointFound(ILogger logger, int count, string endpoint, string name)
            {
                _duplicateEndpointFound(logger, count, endpoint, name, null);
            }
        }
    }
}
