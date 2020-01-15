// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace Microsoft.Azure.SignalR
{
    internal class ServiceScaleManager : ServiceScaleManagerBase
    {
        private readonly ILogger _logger;
        private bool _inited = false;

        private IReadOnlyList<ServiceEndpoint> _endpointsStore = new List<ServiceEndpoint>();
        
        public ServiceScaleManager(IServiceEndpointManager serviceEndpointManager,
            IMultiEndpointServiceContainerManager multiEndpointManager,
            ILoggerFactory loggerFactory,
            IOptionsMonitor<ServiceOptions> optionsMonitor
            ) : base(serviceEndpointManager, multiEndpointManager, loggerFactory) 
        {
            _logger = loggerFactory?.CreateLogger<ServiceScaleManager>();
            OnChange(optionsMonitor.CurrentValue);
            optionsMonitor.OnChange(OnChange);

            _endpointsStore = optionsMonitor.CurrentValue.Endpoints;
            _inited = true;
        }

        private void OnChange(ServiceOptions options)
        {
            // Skip init app starts and respect EnableAutoScale flag
            if (options.EnableAutoScale && _inited)
            {
                Log.DetectEndpointChanges(_logger);

                var endpoints = GetChangedEndpoints(_endpointsStore, options.Endpoints);

                // Add then remove to minor affect to new clients
                OnAdd(endpoints.AddedEndpoints);

                OnRemove(endpoints.RemovedEndpoints);

                // TODO: updated Type Endpoints, do remove then add

                _endpointsStore = options.Endpoints;
            }
        }

        private Task OnAdd(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            return Task.WhenAll(endpoints.ToList().Select(e => AddServiceEndpoint(e)));
        }

        private Task OnRemove(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            return Task.WhenAll(endpoints.ToList().Select(e => RemoveServiceEndpoint(e)));
        }

        private (IReadOnlyList<ServiceEndpoint> AddedEndpoints, IReadOnlyList<ServiceEndpoint> RemovedEndpoints)
            GetChangedEndpoints(IReadOnlyList<ServiceEndpoint> cachedEndpoints, IReadOnlyList<ServiceEndpoint> newEndpoints)
        {
            // Compare by ConnectionString
            var cachedIds = cachedEndpoints.Select(e => e.ConnectionString).ToList();
            var newIds = newEndpoints.Select(e => e.ConnectionString).ToList();

            var addedIds = newIds.Except(cachedIds).ToList();
            var removedIds = cachedIds.Except(newIds).ToList();

            var addedEndpoints = newEndpoints.Where(e => addedIds.Contains(e.ConnectionString)).ToList();
            var removedEndpoints = cachedEndpoints.Where(e => removedIds.Contains(e.ConnectionString)).ToList();

            // TODO: updatedEndpoints

            return (AddedEndpoints: addedEndpoints, RemovedEndpoints: removedEndpoints);
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _detectEndpointChanges =
                LoggerMessage.Define(LogLevel.Debug, new EventId(1, "DetectEndpointChanges"), "Dected endpoint changes in configuration, start live-scale.");

            public static void DetectEndpointChanges(ILogger logger)
            {
                _detectEndpointChanges(logger, null);
            }
        }
    }
}
