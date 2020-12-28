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

        // Filtered valuable endpoints from ServiceOptions, use dict for fast search
        public IReadOnlyDictionary<ServiceEndpoint, ServiceEndpoint> Endpoints { get; private set; }

        public event EndpointEventHandler OnAdd;
        public event EndpointEventHandler OnRemove;
        
        protected ServiceEndpointManagerBase(IServiceEndpointOptions options, ILogger logger)
            : this(ServiceEndpointUtility.Merge(options.ConnectionString, options.Endpoints), logger)
        {
        }

        // for test purpose
        internal ServiceEndpointManagerBase(IEnumerable<ServiceEndpoint> endpoints, ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Endpoints = GetValuableEndpoints(endpoints);
        }

        public abstract IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint);

        public IReadOnlyList<HubServiceEndpoint> GetEndpoints(string hub)
        {
            return _endpointsPerHub.GetOrAdd(hub, s => Endpoints.Select(e => CreateHubServiceEndpoint(hub, e.Key)).ToArray());
        }

        protected Dictionary<ServiceEndpoint, ServiceEndpoint> GetValuableEndpoints(IEnumerable<ServiceEndpoint> endpoints)
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
            }).ToDictionary(k => k, v => v, new ServiceEndpointWeakComparer());

            if (groupedEndpoints.Count == 0)
            {
                throw new AzureSignalRConfigurationNoEndpointException();
            }

            if (groupedEndpoints.Count > 0 && groupedEndpoints.All(s => s.Key.EndpointType != EndpointType.Primary))
            {
                // Only throws when endpoint count > 0
                throw new AzureSignalRNoPrimaryEndpointException();
            }

            return groupedEndpoints;
        }

        protected async virtual Task ReloadServiceEndpointsAsync(IEnumerable<ServiceEndpoint> serviceEndpoints, TimeSpan scaleTimeout)
        {
            try
            {
                var endpoints = GetValuableEndpoints(serviceEndpoints);

                UpdateEndpoints(endpoints, out var addedEndpoints, out var removedEndpoints);

                using (var addCts = new CancellationTokenSource(scaleTimeout))
                {
                    if (!await WaitTaskOrTimeout(AddServiceEndpointsAsync(addedEndpoints, addCts.Token), addCts))
                    {
                        Log.AddEndpointsTimeout(_logger);
                    }
                }

                using (var removeCts = new CancellationTokenSource(scaleTimeout))
                {
                    if (!await WaitTaskOrTimeout(RemoveServiceEndpointsAsync(removedEndpoints, removeCts.Token), removeCts))
                    {
                        Log.RemoveEndpointsTimeout(_logger);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ReloadEndpointsError(_logger, ex);
                return;
            }
        }

        private async Task AddServiceEndpointsAsync(IReadOnlyList<ServiceEndpoint> endpoints, CancellationToken cancellationToken)
        {
            if (endpoints.Count > 0)
            {
                try
                {
                    var hubEndpoints = CreateHubServiceEndpoints(endpoints, true);

                    await Task.WhenAll(hubEndpoints.SelectMany(h => h.Value.Select(e => AddHubServiceEndpointAsync(e, cancellationToken))));

                    AddEndpointsToNegotiationStore(hubEndpoints);
                }
                catch (Exception ex)
                {
                    Log.FailedAddingEndpoints(_logger, ex);
                }
            }
        }

        private async Task RemoveServiceEndpointsAsync(IReadOnlyList<ServiceEndpoint> endpoints, CancellationToken cancellationToken)
        {
            if (endpoints.Count > 0)
            {
                try
                {
                    var hubEndpoints = UpdateAndGetRemovedHubServiceEndpoints(endpoints);

                    await Task.WhenAll(hubEndpoints.Select(e => RemoveHubServiceEndpointAsync(e, cancellationToken)));
                }
                catch (Exception ex)
                {
                    Log.FailedRemovingEndpoints(_logger, ex);
                }
            }
        }

        private void AddEndpointsToNegotiationStore(Dictionary<string, IReadOnlyList<HubServiceEndpoint>> endpoints)
        {
            foreach (var hub in _endpointsPerHub.Keys)
            {
                if (!endpoints.TryGetValue(hub, out var updatedEndpoints) 
                    || updatedEndpoints.Count == 0)
                {
                    return;
                }
                var oldEndpoints = _endpointsPerHub[hub];
                var newEndpoints = oldEndpoints.ToList();
                newEndpoints.AddRange(updatedEndpoints);
                _endpointsPerHub.TryUpdate(hub, newEndpoints, oldEndpoints);
            }
        }

        private IReadOnlyList<HubServiceEndpoint> UpdateAndGetRemovedHubServiceEndpoints(IEnumerable<ServiceEndpoint> endpoints)
        {
            var removedEndpoints = new List<HubServiceEndpoint>();
            foreach (var hub in _endpointsPerHub.Keys)
            {
                var oldEndpoints = _endpointsPerHub[hub];
                var updatedEndpoints = CreateHubServiceEndpoints(hub, endpoints, true);
                removedEndpoints.AddRange(updatedEndpoints);
                var newEndpoints = oldEndpoints.Except(updatedEndpoints, new HubServiceEndpointWeakComparer()).ToList();
                _endpointsPerHub.TryUpdate(hub, newEndpoints, oldEndpoints);
            }
            return removedEndpoints;
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

        private Dictionary<string, IReadOnlyList<HubServiceEndpoint>> CreateHubServiceEndpoints(IEnumerable<ServiceEndpoint> endpoints, bool needScaleTcs)
        {
            var hubEndpoints = new Dictionary<string, IReadOnlyList<HubServiceEndpoint>>();
            foreach (var hub in _endpointsPerHub.Keys)
            {
                hubEndpoints.Add(hub, CreateHubServiceEndpoints(hub, endpoints, needScaleTcs));
            }
            return hubEndpoints;
        }

        private async Task AddHubServiceEndpointAsync(HubServiceEndpoint endpoint, CancellationToken cancellationToken)
        {
            Log.StartAddingEndpoint(_logger, endpoint.Endpoint, endpoint.Name);

            OnAdd?.Invoke(endpoint);

            // Wait for new endpoint turn Ready or timeout getting cancelled
            var task = await Task.WhenAny(endpoint.ScaleTask, cancellationToken.AsTask());

            if (task == endpoint.ScaleTask)
            {
                Log.SucceedAddingEndpoint(_logger, endpoint.ToString());
            }

            // Set complete
            endpoint.CompleteScale();
        }

        private async Task RemoveHubServiceEndpointAsync(HubServiceEndpoint endpoint, CancellationToken cancellationToken)
        {
            Log.StartRemovingEndpoint(_logger, endpoint.Endpoint, endpoint.Name);

            OnRemove?.Invoke(endpoint);

            // Wait for endpoint turn offline or timeout getting cancelled
            var task = await Task.WhenAny(endpoint.ScaleTask, cancellationToken.AsTask());

            if (task == endpoint.ScaleTask)
            {
                Log.SucceedRemovingEndpoint(_logger, endpoint.ToString());
            }

            // Set complete
            endpoint.CompleteScale();
        }

        private void UpdateEndpoints(Dictionary<ServiceEndpoint, ServiceEndpoint> updatedEndpoints,
            out IReadOnlyList<ServiceEndpoint> addedEndpoints,
            out IReadOnlyList<ServiceEndpoint> removedEndpoints)
        {
            var endpoints = new Dictionary<ServiceEndpoint, ServiceEndpoint>();
            var added = new List<ServiceEndpoint>();

            removedEndpoints = Endpoints.Keys.Except(updatedEndpoints.Keys, new ServiceEndpointWeakComparer()).ToList();

            foreach (var endpoint in updatedEndpoints)
            {
                // search exist from old
                if (Endpoints.TryGetValue(endpoint.Key, out var value))
                {
                    // remained or renamed
                    if (value.Name != endpoint.Key.Name)
                    {
                        value.Name = endpoint.Key.Name;
                    }
                    endpoints.Add(value, value);
                }
                else
                {
                    // added
                    endpoints.Add(endpoint.Key, endpoint.Key);
                    added.Add(endpoint.Key);
                }
            }
            addedEndpoints = added;

            Endpoints = endpoints;
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

        private sealed class ServiceEndpointWeakComparer : IEqualityComparer<ServiceEndpoint>
        {
            public bool Equals(ServiceEndpoint x, ServiceEndpoint y)
            {
                return x.Endpoint == y.Endpoint && x.EndpointType == y.EndpointType;
            }

            public int GetHashCode(ServiceEndpoint obj)
            {
                return obj.Endpoint.GetHashCode() ^ obj.EndpointType.GetHashCode();
            }
        }

        private sealed class HubServiceEndpointWeakComparer : IEqualityComparer<HubServiceEndpoint>
        {
            public bool Equals(HubServiceEndpoint x, HubServiceEndpoint y)
            {
                return x.Endpoint == y.Endpoint && x.EndpointType == y.EndpointType;
            }

            public int GetHashCode(HubServiceEndpoint obj)
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

            private static readonly Action<ILogger, Exception> _reloadEndpointError =
                LoggerMessage.Define(LogLevel.Error, new EventId(5, "ReloadEndpointsError"), "No connection string is specified. Skip scale operation.");

            private static readonly Action<ILogger, Exception> _AddEndpointsTimeout =
                LoggerMessage.Define(LogLevel.Error, new EventId(6, "AddEndpointsTimeout"), "Timeout waiting for adding endpoints.");

            private static readonly Action<ILogger, Exception> _removeEndpointsTimeout =
                LoggerMessage.Define(LogLevel.Error, new EventId(7, "RemoveEndpointsTimeout"), "Timeout waiting for removing endpoints.");

            private static readonly Action<ILogger, Exception> _failedAddingEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(8, "FailedAddingEndpoints"), "Failed adding endpoints.");

            private static readonly Action<ILogger, Exception> _failedRemovingEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(9, "FailedRemovingEndpoints"), "Failed removing endpoints.");

            private static readonly Action<ILogger, string, Exception> _succeedAddingEndpoints =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(10, "SucceedAddingEndpoint"), "Succeed in adding endpoint: '{endpoint}'");

            private static readonly Action<ILogger, string, Exception> _succeedRemovingEndpoints =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(11, "SucceedRemovingEndpoint"), "Succeed in removing endpoint: '{endpoint}'");

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

            public static void ReloadEndpointsError(ILogger logger, Exception ex)
            {
                _reloadEndpointError(logger, ex);
            }

            public static void AddEndpointsTimeout(ILogger logger)
            {
                _AddEndpointsTimeout(logger, null);
            }

            public static void RemoveEndpointsTimeout(ILogger logger)
            {
                _removeEndpointsTimeout(logger, null);
            }

            public static void FailedAddingEndpoints(ILogger logger, Exception ex)
            {
                _failedAddingEndpoints(logger, ex);
            }

            public static void FailedRemovingEndpoints(ILogger logger, Exception ex)
            {
                _failedRemovingEndpoints(logger, ex);
            }

            public static void SucceedAddingEndpoint(ILogger logger, string endpoint)
            {
                _succeedAddingEndpoints(logger, endpoint, null);
            }

            public static void SucceedRemovingEndpoint(ILogger logger, string endpoint)
            {
                _succeedRemovingEndpoints(logger, endpoint, null);
            }
        }
    }
}
