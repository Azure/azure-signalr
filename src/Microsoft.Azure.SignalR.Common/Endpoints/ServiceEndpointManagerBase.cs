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
        // Endpoints for negotiation
        private readonly ConcurrentDictionary<string, IReadOnlyList<HubServiceEndpoint>> _endpointsPerHub = new ConcurrentDictionary<string, IReadOnlyList<HubServiceEndpoint>>();

        private readonly ILogger _logger;

        // Filtered valuable endpoints from ServiceOptions
        public ServiceEndpoint[] Endpoints { get; protected set; }

        public event EndpointEventHandler OnAdd;
        public event EndpointEventHandler OnRemove;
        public event EndpointEventHandler OnRename;

        protected ServiceEndpointManagerBase(IServiceEndpointOptions options, ILogger logger) 
            : this(GetEndpoints(options), logger)
        {
        }

        // for test purpose
        internal ServiceEndpointManagerBase(IEnumerable<ServiceEndpoint> endpoints, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Endpoints = GetValuableEndpoints(endpoints);

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

        protected static IEnumerable<ServiceEndpoint> GetEndpoints(IServiceEndpointOptions options)
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

        protected ServiceEndpoint[] GetValuableEndpoints(IEnumerable<ServiceEndpoint> endpoints)
        {
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

            return groupedEndpoints.ToArray();
        }

        protected async Task AddServiceEndpointsAsync(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            if (endpoints.Count > 0)
            {
                var hubEndpoints = CreateHubServiceEndpoints(endpoints);

                await Task.WhenAll(hubEndpoints.Select(e => AddHubServiceEndpointAsync(e)));

                // TODO: update local store for negotiation
            }
        }

        protected async Task RemoveServiceEndpointsAsync(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            if (endpoints.Count > 0)
            {
                var hubEndpoints = CreateHubServiceEndpoints(endpoints);

                // TODO: update local store for negotiation

                await Task.WhenAll(hubEndpoints.Select(e => RemoveHubServiceEndpointAsync(e)));
            }
        }

        protected Task RenameSerivceEndpoints(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            if (endpoints.Count > 0)
            {
                var hubEndpoints = CreateHubServiceEndpoints(endpoints);

                // TODO: update local store for negotiation

                return Task.WhenAll(hubEndpoints.Select(e => RenameHubServiceEndpoint(e)));
            }
            return Task.CompletedTask;
        }

        private HubServiceEndpoint CreateHubServiceEndpoint(string hub, ServiceEndpoint endpoint)
        {
            var provider = GetEndpointProvider(endpoint);
            return new HubServiceEndpoint(hub, provider, endpoint);
        }

        private IReadOnlyList<HubServiceEndpoint> CreateHubServiceEndpoints(string hub, IEnumerable<ServiceEndpoint> endpoints)
        {
            return endpoints.Select(e => CreateHubServiceEndpoint(hub, e)).ToList();
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

        private async Task AddHubServiceEndpointAsync(HubServiceEndpoint endpoint)
        {
            Log.StartAddingEndpoint(_logger, endpoint.Endpoint, endpoint.Name);
            endpoint.Ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            OnAdd?.Invoke(endpoint);
            // Wait for new endpoint turn Ready
            await endpoint.Ready.Task;
        }

        private async Task RemoveHubServiceEndpointAsync(HubServiceEndpoint endpoint)
        {
            Log.StartRemovingEndpoint(_logger, endpoint.Endpoint, endpoint.Name);
            endpoint.Offline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            OnRemove?.Invoke(endpoint);
            // Wait for endpoint turn Offline
            await endpoint.Offline.Task;
        }

        private Task RenameHubServiceEndpoint(HubServiceEndpoint endpoint)
        {
            Log.StartAddingEndpoint(_logger, endpoint.Endpoint, endpoint.Name);

            OnRename?.Invoke(endpoint);
            return Task.CompletedTask;
        }

        private static class Log
        {
            private static readonly Action<ILogger, int, string, string, Exception> _duplicateEndpointFound =
                LoggerMessage.Define<int, string, string>(LogLevel.Warning, new EventId(1, "DuplicateEndpointFound"), "{count} endpoint configurations to '{endpoint}' found, use '{name}'.");

            private static readonly Action<ILogger, string, string, Exception> _startAddingEndpoint =
                LoggerMessage.Define< string, string>(LogLevel.Debug, new EventId(2, "StartAddingEndpoint"), "Start adding endpoint: '{endpoint}', name: '{name}'.");

            private static readonly Action<ILogger, string, string, Exception> _startRemovingEndpoint =
                LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(3, "StartRemovingEndpoint"), "Start removing endpoint: '{endpoint}', name: '{name}'");
            
            private static readonly Action<ILogger, string, string, Exception> _startRenamingEndpoint =
                LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(4, "StartRenamingEndpoint"), "Start renaming endpoint: '{endpoint}', name: '{name}'");


            public static void DuplicateEndpointFound(ILogger logger, int count, string endpoint, string name)
            {
                _duplicateEndpointFound(logger, count, endpoint, name, null);
            }

            public static void StartAddingEndpoint(ILogger logger, string endpoint, string name)
            {
                _startAddingEndpoint(logger, endpoint, name, null);
            }

            public static void StartRemovingEndpoint(ILogger logger, string endpoint, string name)
            {
                _startRemovingEndpoint(logger, endpoint, name, null);
            }

            public static void StartRenamingEndpoint(ILogger logger, string endpoint, string name)
            {
                _startRenamingEndpoint(logger, endpoint, name, null);
            }
        }
    }
}
