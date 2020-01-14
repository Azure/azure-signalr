using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceContainerFactory : IMultiEndpointServiceContainerFactory
    {
        private readonly ConcurrentDictionary<string, IMultiEndpointServiceConnectionContainer> _hubContainers = new ConcurrentDictionary<string, IMultiEndpointServiceConnectionContainer>();
        
        public void AddMultipleEndpointServiceConnectionContainer(string hub, IMultiEndpointServiceConnectionContainer container)
        {
            if (!_hubContainers.TryAdd(hub, container))
            {
                // log duplicate
            }
        }

        public IEnumerable<string> GetHubs()
        {
            return _hubContainers.Keys;
        }

        public bool TryGetMultiEndpointServiceConnection(string hub, out IMultiEndpointServiceConnectionContainer container)
        {
            return _hubContainers.TryGetValue(hub, out container);
        }
    }
}
