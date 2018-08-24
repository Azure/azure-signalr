// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionManager : IServiceConnectionManager
    {
        private IReadOnlyDictionary<string, IServiceConnectionContainer> _serviceConnections = null;
        private readonly object _lock = new object();

        private readonly IReadOnlyList<string> _hubs;

        public ServiceConnectionManager(IReadOnlyList<string> hubs)
        {
            _hubs = hubs;
        }

        public Task StartAsync(Func<string, IServiceConnection> connectionGenerator, int connectionCount)
        {
            if (connectionGenerator == null)
            {
                throw new ArgumentNullException(nameof(connectionGenerator));
            }

            if (connectionCount <= 0)
            {
                throw new ArgumentException($"{nameof(connectionCount)} must be larger than 0.");
            }

            if (_serviceConnections != null)
            {
                // TODO: log something to indicate the connection is already established?
                return Task.CompletedTask;
            }

            lock (_lock)
            {
                if (_serviceConnections != null)
                {
                    return Task.CompletedTask;
                }

                var connections = new Dictionary<string, IServiceConnectionContainer>();
                foreach (var hub in _hubs)
                {
                    var connection = new ServiceConnectionContainer(
                            () => connectionGenerator(hub),
                            connectionCount);
                    connections.Add(hub, connection);
                }

                _serviceConnections = connections;
            }

            return StartAsync();
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
