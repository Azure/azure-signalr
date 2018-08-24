// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionManager : IServiceConnectionManager
    {
        private readonly ConcurrentDictionary<string, IServiceConnectionContainer> _serviceConnections = new ConcurrentDictionary<string, IServiceConnectionContainer>();
                
        public void AddConnection(string hubName, IServiceConnectionContainer connection)
        {
            _serviceConnections.TryAdd(hubName, connection);
        }

        public Task StartAsync()
        {
            return Task.WhenAll(GetConnections().Select(s => s.StartAsync()));
        }

        public Task StopAsync()
        {
            return Task.WhenAll(GetConnections().Select(s => s.StopAsync()));
        }

        public IServiceConnectionContainer WithHub(string hubName)
        {
            if (!_serviceConnections.TryGetValue(hubName, out var connection))
            {
                throw new KeyNotFoundException($"Service connection with Hub {hubName} does not exist");
            }

            return connection;
        }

        public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            return Task.WhenAll(GetConnections().Select(s => s.WriteAsync(partitionKey, serviceMessage)));
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            return Task.WhenAll(GetConnections().Select(s => s.WriteAsync(serviceMessage)));
        }

        private IEnumerable<IServiceConnectionContainer> GetConnections()
        {
            foreach(var pair in _serviceConnections)
            {
                yield return pair.Value;
            }
        }
    }
}
