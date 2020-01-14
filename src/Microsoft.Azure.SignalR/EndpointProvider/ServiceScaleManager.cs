using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceScaleManager : IServiceScaleManager
    {
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly IMultiEndpointServiceContainerFactory _multiEndpointServiceContainerFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private bool _inited = false;
        private static readonly TimeSpan DefaultScaleTimeout = TimeSpan.FromMinutes(15);

        private IReadOnlyList<ServiceEndpoint> _endpointsStore = new List<ServiceEndpoint>();
        
        public ServiceScaleManager(IServiceEndpointManager serviceEndpointManager,
            IMultiEndpointServiceContainerFactory multiEndpointServiceContainerFactory,
            ILoggerFactory loggerFactory,
            IOptionsMonitor<ServiceOptions> optionsMonitor)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)); ;
            _logger = loggerFactory?.CreateLogger<ServiceScaleManager>();
            _serviceEndpointManager = serviceEndpointManager;
            _multiEndpointServiceContainerFactory = multiEndpointServiceContainerFactory;

            OnChange(optionsMonitor.CurrentValue);
            optionsMonitor.OnChange(OnChange);

            _endpointsStore = optionsMonitor.CurrentValue.Endpoints;
            _inited = true;
        }

        public async Task AddServiceEndpoint(ServiceEndpoint endpoint)
        {
            var hubs = _multiEndpointServiceContainerFactory.GetHubs();
            var isCreated = new List<bool>();
            foreach (var hub in hubs)
            {
                _multiEndpointServiceContainerFactory.TryGetMultiEndpointServiceConnection(hub, out var container);
                var hubEndpoint = _serviceEndpointManager.GenerateHubServiceEndpoint(hub, endpoint);
                if(await container.TryAddServiceEndpoint(hubEndpoint))
                {
                    // container.StartAddServiceEndpoint()
                }
            }

            // 1. per hub get MultiSEContainer
            // 2. MultiSEContainer.TryAddServiceEndpoint()
            // 3. When all success, call MultiSEContainer.StartAddServiceEndpoint() to trigger service ping to get global server ids
            // 4. Wait MultiSEContainer.IsStable() to check server connection setup successfully
            // 5. MultiSEContainer.StopAddServiceEndpoint() to stop ping
            // 6. ServiceEndpointManager.AddServiceEndpointToNegotiation() to enable negotiation in server side.
            throw new NotImplementedException();
        }

        public Task RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            // 1. ServiceEndpointManager.RemoveServiceEndpointFromNegotiation()
            // 2. MultiSEContainer.StartRemoveServiceEndpoint() to trigger `Fin` ping.
            // 3. Wait MultiSEContainer.HasClients() to check clients for the endpoint are diconnected
            // 4. MultiSEContainer.TryRemoveServiceEndpoint() to stop server connections and remove local container
            throw new NotImplementedException();
        }

        public IEnumerable<ServiceEndpoint> GetServiceEndpoints(string hub)
        {
            return _serviceEndpointManager.GetEndpoints(hub);
        }

        private void OnChange(ServiceOptions options)
        {
            // Skip init app starts and respect EnableAutoScale flag
            if (options.EnableAutoScale && _inited)
            {
                var endpoints = GetChangedEndpoints(_endpointsStore, options.Endpoints);

                // Do add then remove
                OnAdd(endpoints.AddedEndpoints);

                OnRemove(endpoints.RemovedEndpoints);

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

        private async Task<bool> WaitForConnectionStart()
        {
            var startWait = DateTime.UtcNow;
            while (DateTime.UtcNow - startWait < DefaultScaleTimeout)
            {
                var hubs = _multiEndpointServiceContainerFactory.GetHubs();
                foreach (var hub in hubs)
                {
                    // if succeed
                    return true;
                }
                // wait 1s for next try
                await Task.Delay(5000);
            }
            return false;
        }
    }
}
