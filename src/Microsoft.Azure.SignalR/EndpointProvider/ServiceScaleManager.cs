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
    internal class ServiceScaleManager
    {
        private readonly ILogger _logger;
        private bool _inited = false;

        private IReadOnlyList<ServiceEndpoint> _endpointsStore = new List<ServiceEndpoint>();

        public ServiceScaleManager(ILoggerFactory loggerFactory,
            IOptionsMonitor<ServiceOptions> optionsMonitor)
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

                var updatedEndpoints = options.Endpoints;
                var endpoints = GetChangedEndpoints(updatedEndpoints);

                // Add then remove to minor affect to new clients
                OnAdd(endpoints.AddedEndpoints);

                OnRemove(endpoints.RemovedEndpoints);

                // TODO: updated Type Endpoints
                // For EndpointType, do remove then add. 
                // For name, update properties in client/message router.

                _endpointsStore = updatedEndpoints;
            }
        }

        private Task OnAdd(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            throw new NotImplementedException();
        }

        private Task OnRemove(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            throw new NotImplementedException();
        }

        private (IReadOnlyList<ServiceEndpoint> AddedEndpoints, IReadOnlyList<ServiceEndpoint> RemovedEndpoints)
            GetChangedEndpoints(IReadOnlyList<ServiceEndpoint> updatedEndpoints)
        {
            // Compare by ConnectionString
            var cachedIds = _endpointsStore.Select(e => e.ConnectionString).ToList();
            var newIds = updatedEndpoints.Select(e => e.ConnectionString).ToList();

            var addedIds = newIds.Except(cachedIds).ToList();
            var removedIds = cachedIds.Except(newIds).ToList();

            var addedEndpoints = updatedEndpoints.Where(e => addedIds.Contains(e.ConnectionString)).ToList();
            var removedEndpoints = _endpointsStore.Where(e => removedIds.Contains(e.ConnectionString)).ToList();

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
