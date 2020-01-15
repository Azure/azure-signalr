// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceContainerManager : IMultiEndpointServiceContainerManager
    {
        private readonly ConcurrentDictionary<string, IMultiEndpointServiceConnectionContainer> _hubContainers = new ConcurrentDictionary<string, IMultiEndpointServiceConnectionContainer>();

        public bool TryGet(string hub, out IMultiEndpointServiceConnectionContainer container)
        {
            return _hubContainers.TryGetValue(hub, out container);
        }

        public IReadOnlyList<string> Hubs { get; private set; } = new List<string>();

        public void SaveMultipleEndpointServiceConnectionContainer(string hub, IMultiEndpointServiceConnectionContainer container)
        {
            _hubContainers.TryAdd(hub, container);
            Hubs = _hubContainers.Select(h => h.Key).ToList();
        }
    }
}
