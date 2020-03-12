﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        public HashSet<ServiceEndpoint> Endpoints { get; private set; } = new HashSet<ServiceEndpoint>(ServiceEndpointWeakComparer.Instance);

        public event EndpointEventHandler OnAdd;
        public event EndpointEventHandler OnRemove;

        protected ServiceEndpointManagerBase(IServiceEndpointOptions options, ILogger logger) 
            : this(GetEndpoints(options), logger)
        {
        }

        // for test purpose
        internal ServiceEndpointManagerBase(IEnumerable<ServiceEndpoint> endpoints, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Endpoints = GetValuableEndpoints(endpoints);

            if (Endpoints.Count > 0 && Endpoints.All(s => s.EndpointType != EndpointType.Primary))
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

        protected HashSet<ServiceEndpoint> GetValuableEndpoints(IEnumerable<ServiceEndpoint> endpoints)
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

            return new HashSet<ServiceEndpoint>(groupedEndpoints, ServiceEndpointWeakComparer.Instance);
        }

        protected async Task ReloadServiceEndpointsAsync(ServiceEndpoint[] serviceEndpoints, TimeSpan scaleTimeout)
        {
            var endpoints = GetValuableEndpoints(serviceEndpoints);
            // Follow same check but log error only
            if (endpoints.Count == 0)
            {
                Log.EndpointNotFound(_logger);
                return;
            }
            if (endpoints.Count > 0 && endpoints.All(s => s.EndpointType != EndpointType.Primary))
            {
                Log.NoPrimaryEndpoint(_logger);
                return;
            }

            UpdateEndpoints(endpoints, out var addedEndpoints, out var removedEndpoints);

            using (var addCts = new CancellationTokenSource(scaleTimeout))
            {
                if (!await WaitTaskOrTimeout(AddServiceEndpointsAsync(addedEndpoints, addCts.Token), addCts))
                {
                    Log.TimeoutAddEndpoints(_logger);
                }
            }

            using (var removeCts = new CancellationTokenSource(scaleTimeout))
            {
                if (!await WaitTaskOrTimeout(RemoveServiceEndpointsAsync(removedEndpoints, removeCts.Token), removeCts))
                {
                    Log.TimeoutRemoveEndpoints(_logger);
                }
            }
        }

        private async Task AddServiceEndpointsAsync(IEnumerable<ServiceEndpoint> endpoints, CancellationToken cancellationToken)
        {
            if (endpoints.Count() > 0)
            {
                try
                {
                    var hubEndpoints = CreateHubServiceEndpoints(endpoints, true);

                    await Task.WhenAll(hubEndpoints.Select(e => AddHubServiceEndpointAsync(e, cancellationToken)));

                    // TODO: update local store for negotiation
                }
                catch (Exception ex)
                {
                    Log.FailedAddingEndpoints(_logger, ex);
                }
            }
        }

        private async Task RemoveServiceEndpointsAsync(IEnumerable<ServiceEndpoint> endpoints, CancellationToken cancellationToken)
        {
            if (endpoints.Count() > 0)
            {
                try
                {
                    var hubEndpoints = CreateHubServiceEndpoints(endpoints, true);

                    // TODO: update local store for negotiation

                    await Task.WhenAll(hubEndpoints.Select(e => RemoveHubServiceEndpointAsync(e, cancellationToken)));
                }
                catch (Exception ex)
                {
                    Log.FailedRemovingEndpoints(_logger, ex);
                }
            }
        }

        private HubServiceEndpoint CreateHubServiceEndpoint(string hub, ServiceEndpoint endpoint, bool needScaleTcs = false)
        {
            var provider = GetEndpointProvider(endpoint);

            return new HubServiceEndpoint(hub, provider, endpoint, needScaleTcs);
        }

        private IReadOnlyList<HubServiceEndpoint> CreateHubServiceEndpoints(string hub, IEnumerable<ServiceEndpoint> endpoints, bool needScaleTcs)
        {
            return endpoints.Select(e => CreateHubServiceEndpoint(hub, e, needScaleTcs)).ToList();
        }

        private IReadOnlyList<HubServiceEndpoint> CreateHubServiceEndpoints(IEnumerable<ServiceEndpoint> endpoints, bool needScaleTcs)
        {
            var hubEndpoints = new List<HubServiceEndpoint>();
            var hubs = _endpointsPerHub.Keys;
            foreach (var hub in hubs)
            {
                hubEndpoints.AddRange(CreateHubServiceEndpoints(hub, endpoints, needScaleTcs));
            }
            return hubEndpoints;
        }

        private async Task AddHubServiceEndpointAsync(HubServiceEndpoint endpoint, CancellationToken cancellationToken)
        {
            Log.StartAddingEndpoint(_logger, endpoint.Endpoint, endpoint.Name);

            OnAdd?.Invoke(endpoint);

            // Wait for new endpoint turn Ready or timeout getting cancelled
            await Task.WhenAny(endpoint.ScaleTask, cancellationToken.AsTask());

            // Set complete
            endpoint.CompleteScale();
        }

        private async Task RemoveHubServiceEndpointAsync(HubServiceEndpoint endpoint, CancellationToken cancellationToken)
        {
            Log.StartRemovingEndpoint(_logger, endpoint.Endpoint, endpoint.Name);

            OnRemove?.Invoke(endpoint);

            // Wait for endpoint turn offline or timeout getting cancelled
            await Task.WhenAny(endpoint.ScaleTask, cancellationToken.AsTask());

            // Set complete
            endpoint.CompleteScale();
        }

        private void UpdateEndpoints(HashSet<ServiceEndpoint> updatedEndpoints,
            out HashSet<ServiceEndpoint> addedEndpoints,
            out HashSet<ServiceEndpoint> removedEndpoints)
        {
            // Provide a copy of Endpoints to calculate removed
            removedEndpoints = new HashSet<ServiceEndpoint>(Endpoints, ServiceEndpointWeakComparer.Instance);
            addedEndpoints = new HashSet<ServiceEndpoint>();

            foreach (var endpoint in updatedEndpoints)
            {
                if (Endpoints.Contains(endpoint))
                {
                    // Directly update name in original sets for renamed ones
                    var oldValue = (Endpoints.Comparer as ServiceEndpointWeakComparer).Value;
                    if (oldValue.Name != endpoint.Name)
                    {
                        oldValue.Name = endpoint.Name;
                    }
                }
                else
                {
                    // added news
                    Endpoints.Add(endpoint);
                    addedEndpoints.Add(endpoint);
                }
            }
            
            removedEndpoints.ExceptWith(updatedEndpoints);
            Endpoints.ExceptWith(removedEndpoints);
        }

        private static async Task<bool> WaitTaskOrTimeout(Task task, CancellationTokenSource cts)
        {
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));

            if (completed == task)
            {
                return true;
            }

            cts.Cancel();
            return false;
        }

        protected sealed class ServiceEndpointWeakComparer : IEqualityComparer<ServiceEndpoint>
        {
            public static readonly ServiceEndpointWeakComparer Instance = new ServiceEndpointWeakComparer();

            public ServiceEndpoint Value { get; private set; }

            public bool Equals(ServiceEndpoint x, ServiceEndpoint y)
            {
                if (x.Endpoint == y.Endpoint && x.EndpointType == y.EndpointType)
                {
                    Value = x;
                    return true;
                }
                return false;
            }

            public int GetHashCode(ServiceEndpoint obj)
            {
                return obj.Endpoint.GetHashCode() ^ obj.EndpointType.GetHashCode();
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, int, string, string, Exception> _duplicateEndpointFound =
                LoggerMessage.Define<int, string, string>(LogLevel.Warning, new EventId(1, "DuplicateEndpointFound"), "{count} endpoint configurations to '{endpoint}' found, use '{name}'.");

            private static readonly Action<ILogger, string, string, Exception> _startAddingEndpoint =
                LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(2, "StartAddingEndpoint"), "Start adding endpoint: '{endpoint}', name: '{name}'.");
            
            private static readonly Action<ILogger, string, string, Exception> _startRemovingEndpoint =
                LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(3, "StartRemovingEndpoint"), "Start removing endpoint: '{endpoint}', name: '{name}'");

            private static readonly Action<ILogger, string, string, Exception> _startRenamingEndpoint =
                LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(4, "StartRenamingEndpoint"), "Start renaming endpoint: '{endpoint}', name: '{name}'");

            private static readonly Action<ILogger, Exception> _endpointNotFound =
                LoggerMessage.Define(LogLevel.Error, new EventId(5, "EndpointNotFound"), "No connection string is specified. Skip scale operation.");
            
            private static readonly Action<ILogger, Exception> _noPrimaryEndpoint =
                LoggerMessage.Define(LogLevel.Error, new EventId(6, "NoPrimaryEndpoint"), "No primary endpoint found. Skip scale operation.");

            private static readonly Action<ILogger, Exception> _timeoutAddEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(7, "TimeoutAddEndpoints"), "Timeout waiting for adding endpoints.");

            private static readonly Action<ILogger, Exception> _timeoutRemoveEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(8, "TimeoutRemoveEndpoints"), "Timeout waiting for removing endpoints.");

            private static readonly Action<ILogger, Exception> _failedAddingEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(9, "FailedAddingEndpoints"), "Failed adding endpoints.");

            private static readonly Action<ILogger, Exception> _failedRemovingEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(10, "FailedRemovingEndpoints"), "Failed removing endpoints.");

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

            public static void EndpointNotFound(ILogger logger)
            {
                _endpointNotFound(logger, null);
            }

            public static void NoPrimaryEndpoint(ILogger logger)
            {
                _noPrimaryEndpoint(logger, null);
            }

            public static void TimeoutAddEndpoints(ILogger logger)
            {
                _timeoutAddEndpoints(logger, null);
            }

            public static void TimeoutRemoveEndpoints(ILogger logger)
            {
                _timeoutRemoveEndpoints(logger, null);
            }

            public static void FailedAddingEndpoints(ILogger logger, Exception ex)
            {
                _failedAddingEndpoints(logger, ex);
            }

            public static void FailedRemovingEndpoints(ILogger logger, Exception ex)
            {
                _failedRemovingEndpoints(logger, ex);
            }
        }
    }
}
