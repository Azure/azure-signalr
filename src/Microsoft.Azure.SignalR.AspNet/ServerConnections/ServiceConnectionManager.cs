// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionManager : IServiceConnectionManager
    {
        private IReadOnlyDictionary<string, IServiceConnectionContainer> _serviceConnections = null;

        private readonly object _lock = new object();

        private readonly IReadOnlyList<string> _hubs;
        private readonly string _appName;

        private IServiceConnectionContainer _appConnection;

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public ServiceConnectionManager(string appName, IReadOnlyList<string> hubs)
        {
            if (hubs.Contains(appName))
            {
                throw new ArgumentException("App name should not be the same as hub name.");
            }

            _hubs = hubs;
            _appName = appName;
        }

        public void Initialize(Func<string, IServiceConnectionContainer> connectionGenerator)
        {
            if (connectionGenerator == null)
            {
                throw new ArgumentNullException(nameof(connectionGenerator));
            }

            if (_serviceConnections != null)
            {
                // TODO: log something to indicate the connection is already initialized.
                return;
            }

            lock (_lock)
            {
                if (_serviceConnections != null)
                {
                    return;
                }

                var connections = new Dictionary<string, IServiceConnectionContainer>();

                _appConnection = connectionGenerator(_appName);

                foreach (var hub in _hubs)
                {
                    var connection = connectionGenerator(hub);
                    connections.Add(hub, connection);
                }

                _serviceConnections = connections;
            }
        }

        public Task StartAsync()
        {
            return Task.WhenAll(GetConnections().Select(s => s.StartAsync()));
        }

        public IServiceConnectionContainer WithHub(string hubName)
        {
            if (_serviceConnections == null ||!_serviceConnections.TryGetValue(hubName, out var connection))
            {
                throw new KeyNotFoundException($"Service connection with Hub {hubName} does not exist");
            }

            return connection;
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_appConnection == null)
            {
                throw new InvalidOperationException("App connection is not yet initialized.");
            }

            return _appConnection.WriteAsync(serviceMessage);
        }

        public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            if (_appConnection == null)
            {
                throw new InvalidOperationException("App connection is not yet initialized.");
            }

            return _appConnection.WriteAsync(partitionKey, serviceMessage);
        }

        public Task WriteWithAckAsync(ServiceMessage serviceMessage, string guid, TaskCompletionSource<bool> tcs)
        {
            if (_appConnection == null)
            {
                throw new InvalidOperationException("App connection is not yet initialized.");
            }

            return _appConnection.WriteWithAckAsync(serviceMessage, guid, tcs);
        }

        private IEnumerable<IServiceConnectionContainer> GetConnections()
        {
            if (_appConnection != null)
            {
                yield return _appConnection;
            }

            if (_serviceConnections != null)
            {
                foreach (var conn in _serviceConnections)
                {
                    yield return conn.Value;
                }
            }
        }
    }
}
