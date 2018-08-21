// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionManager : IServiceConnectionManager
    {
        private const char DotChar = '.';

        private IReadOnlyDictionary<string, IServiceConnectionContainer> _serviceConnections = null;
        private readonly HashSet<string> _hubNameWithDots = new HashSet<string>();

        private readonly object _lock = new object();

        private readonly IReadOnlyList<string> _hubs;
        private readonly string _appName;

        private IServiceConnectionContainer _appConnection;

        public ServiceConnectionManager(string appName, IReadOnlyList<string> hubs)
        {
            _hubs = hubs;
            _appName = appName;
        }

        public void Initialize(Func<string, IServiceConnection> connectionGenerator, int connectionCount)
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

                _appConnection = new ServiceConnectionContainer(
                        () => connectionGenerator(_appName),
                        connectionCount);

                foreach (var hub in _hubs)
                {
                    var connection = new ServiceConnectionContainer(
                            () => connectionGenerator(hub),
                            connectionCount);
                    connections.Add(hub, connection);

                    // It is possible that the hub contains dot character, while the fully qualified name is formed as {HubName}.{Name} (Name can be connectionId or userId or groupId)
                    // So keep a copy of the hub names containing dots and return all the possible combinations when the fully qualified name is provided
                    if (hub.IndexOf(DotChar) > -1)
                    {
                        lock (_hubNameWithDots)
                        {
                            _hubNameWithDots.Add(hub);
                        }
                    }
                }

                _serviceConnections = connections;
            }
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

        /// <summary>
        /// The fully qualified name is as {HubName}.{Name}
        /// </summary>
        /// <param name="nameWithHubPrefix"></param>
        /// <returns>The connection and the name without hub prefix</returns>
        public IEnumerable<(IServiceConnectionContainer, string)> GetPossibleConnections(string nameWithHubPrefix)
        {
            var index = nameWithHubPrefix.IndexOf(DotChar);
            if (index == -1)
            {
                throw new InvalidDataException($"Name {nameWithHubPrefix} does not contain the required separator {DotChar}");
            }

            // It is rare that hubname contains '.'
            foreach (var hub in _hubNameWithDots)
            {
                if (nameWithHubPrefix.Length > hub.Length + 1
                    && nameWithHubPrefix[hub.Length] == DotChar
                    && hub == nameWithHubPrefix.Substring(0, hub.Length))
                {
                    yield return (_serviceConnections[hub], nameWithHubPrefix.Substring(hub.Length + 1));
                }
            }

            var hubName = nameWithHubPrefix.Substring(0, index);
            var name = nameWithHubPrefix.Substring(index + 1);
            yield return (WithHub(hubName), name);
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
