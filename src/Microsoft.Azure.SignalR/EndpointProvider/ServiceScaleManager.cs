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
        private IReadOnlyList<ServiceEndpoint> _endpointsStore;

        public ServiceScaleManager(ILoggerFactory loggerFactory,
            IOptionsMonitor<ServiceOptions> optionsMonitor)
        {
            _logger = loggerFactory?.CreateLogger<ServiceScaleManager>();

            // Disable optionsMonitor until feature ready.
            // optionsMonitor.OnChange(OnChange);

            _endpointsStore = optionsMonitor.CurrentValue.Endpoints;
        }

        private void OnChange(ServiceOptions options)
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
            var addedEndpoints = updatedEndpoints.Except(_endpointsStore).ToList();
            var removedEndpoints = _endpointsStore.Except(updatedEndpoints).ToList();

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
