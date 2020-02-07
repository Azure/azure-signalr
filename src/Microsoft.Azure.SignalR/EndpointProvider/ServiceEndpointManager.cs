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
    internal class ServiceEndpointManager : ServiceEndpointManagerBase
    {
        private readonly ILogger _logger;

        // Store the initial ServiceOptions for generate EndpointProvider use.
        // Only Endpoints value accept hot-reload and prevent changes of unexpected modification on other configurations.
        private readonly ServiceOptions _options;
        private IReadOnlyList<ServiceEndpoint> _endpointsStore;

        public ServiceEndpointManager(IOptionsMonitor<ServiceOptions> optionsMonitor, ILoggerFactory loggerFactory) :
            base(optionsMonitor.CurrentValue, loggerFactory.CreateLogger<ServiceEndpointManager>())
        {
            if (Endpoints.Length == 0)
            {
                throw new ArgumentException(ServiceEndpointProvider.ConnectionStringNotFound);
            }
            _options = optionsMonitor.CurrentValue;
            _logger = loggerFactory?.CreateLogger<ServiceEndpointManager>();

            // TODO: Enable optionsMonitor.OnChange when feature ready.
            // optionsMonitor.OnChange(OnChange);
            _endpointsStore = Endpoints.ToList();
        }

        public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                return null;
            }

            return new ServiceEndpointProvider(endpoint, _options);
        }

        private void OnChange(ServiceOptions options)
        {
            Log.DetectConfigurationChanges(_logger);

            // Reset local cache
            SetValuableEndpoints(GetEndpoints(options));
            var updatedEndpoints = Endpoints.ToList();

            var endpoints = GetChangedEndpoints(updatedEndpoints);

            OnRename(endpoints.RenamedEndpoints);

            _ = DoScaleAsync(endpoints.AddedEndpoints, endpoints.RemovedEndpoints);

            _endpointsStore = updatedEndpoints.ToList();
        }

        private Task DoScaleAsync(IReadOnlyList<ServiceEndpoint> addedEndpoints, IReadOnlyList<ServiceEndpoint> removedEndpoints)
        {
            // First add then remove to minor the affect to new clients
            OnAdd(addedEndpoints);

            OnRemove(removedEndpoints);

            return Task.CompletedTask;
        }

        private Task OnAdd(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            // TODO: parallel do add
            return Task.CompletedTask;
        }

        private Task OnRemove(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            // TODO: parallel do remove
            return Task.CompletedTask;
        }

        private void OnRename(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            // No need to affect existing connections, property update is enough
        }

        private (IReadOnlyList<ServiceEndpoint> AddedEndpoints, 
            IReadOnlyList<ServiceEndpoint> RemovedEndpoints,
            IReadOnlyList<ServiceEndpoint> RenamedEndpoints)
            GetChangedEndpoints(IEnumerable<ServiceEndpoint> updatedEndpoints)
        {
            var addedEndpoints = updatedEndpoints.Except(_endpointsStore, new ServiceEndpointWeakComparer()).ToList();
            var removedEndpoints = _endpointsStore.Except(updatedEndpoints, new ServiceEndpointWeakComparer()).ToList();

            var renamedEndpoints = updatedEndpoints.Except(_endpointsStore).Except(addedEndpoints).ToList();

            return (AddedEndpoints: addedEndpoints, RemovedEndpoints: removedEndpoints, RenamedEndpoints: renamedEndpoints);
        }

        private sealed class ServiceEndpointWeakComparer : IEqualityComparer<ServiceEndpoint>
        {
            public bool Equals(ServiceEndpoint x, ServiceEndpoint y)
            {
                return x.ConnectionString == y.ConnectionString && x.EndpointType == y.EndpointType;
            }

            public int GetHashCode(ServiceEndpoint obj)
            {
                return obj.ConnectionString.GetHashCode() ^ obj.EndpointType.GetHashCode();
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _detectEndpointChanges =
                LoggerMessage.Define(LogLevel.Debug, new EventId(1, "DetectConfigurationChanges"), "Dected configuration changes in configuration, start live-scale.");

            public static void DetectConfigurationChanges(ILogger logger)
            {
                _detectEndpointChanges(logger, null);
            }
        }
    }
}
