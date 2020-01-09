using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceScaleManager : IServiceScaleManager
    {
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private bool _inited = false;

        private IReadOnlyList<ServiceEndpoint> _store = new List<ServiceEndpoint>();
        private readonly ConcurrentDictionary<string, IMultiEndpointServiceConnectionContainer> _hubContainers = new ConcurrentDictionary<string, IMultiEndpointServiceConnectionContainer>();
        
        public ServiceScaleManager(IServiceEndpointManager serviceEndpointManager,
            IClientConnectionManager clientConnectionManager,
            ILoggerFactory loggerFactory,
            IOptionsMonitor<ServiceOptions> optionsMonitor)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)); ;
            _logger = loggerFactory?.CreateLogger<ServiceScaleManager>();
            _serviceEndpointManager = serviceEndpointManager;

            OnChange(optionsMonitor.CurrentValue);
            optionsMonitor.OnChange(OnChange);

            _store = optionsMonitor.CurrentValue.Endpoints;
            _inited = true;
        }
        
        public void AddMultipleEndpointServiceConnectionContainer(IMultiEndpointServiceConnectionContainer container)
        {
            throw new NotImplementedException();
        }

        public Task AddServiceEndpoint(ServiceEndpoint endpoint)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<string> GetHubs()
        {
            throw new NotImplementedException();
        }

        public IMultiEndpointServiceConnectionContainer GetMultipleEndpointServiceConnectionContainer(string hub)
        {
            throw new NotImplementedException();
        }

        public Task RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            throw new NotImplementedException();
        }

        private void OnChange(ServiceOptions options)
        {
            // Skip init app starts and respect EnableAutoScale flag
            if (options.EnableAutoScale && _inited)
            {
                var endpoints = GetChangedEndpoints(_store, options.Endpoints);

                // Do add then remove
                OnAdd(endpoints.AddedEndpoints);

                OnRemove(endpoints.RemovedEndpoints);

                _store = options.Endpoints;
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

            return (AddedEndpoints: addedEndpoints, RemovedEndpoints: removedEndpoints);
        }
    }
}
