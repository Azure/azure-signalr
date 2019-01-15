// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class StrongServiceConnectionContainer : IServiceConnectionContainer
    {
        private static readonly int MaxReconnectBackoffInternalInMilliseconds = 1000;
        private static TimeSpan ReconnectInterval =>
            TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackoffInternalInMilliseconds));

        private readonly List<IServiceConnection> _serviceConnections;
        private readonly IServiceConnectionFactory _serviceConnectionFactory;
        private readonly IConnectionFactory _connectionFactory;
        private readonly int _defaultConnectionCount;
        private readonly object _lock = new object();
        private volatile int _defaultConnectionRetry;
        private DateTime _defaultConnectionReconnectionTime = DateTime.UtcNow;

        public StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory,
            int defaultConnectionCount)
        {
            _defaultConnectionCount = defaultConnectionCount;
            _serviceConnectionFactory = serviceConnectionFactory;
            _serviceConnections = new List<IServiceConnection>();
            _connectionFactory = connectionFactory;
        }

        public Task Initialize()
        {
            var connections = CreateServiceConnection(_defaultConnectionCount);
            return Task.WhenAll(connections.Select(c => c.StartAsync()));
        }

        public IEnumerable<IServiceConnection> CreateServiceConnection(int count = 1)
        {
            if (count <= 0)
            {
                throw new ArgumentException($"{nameof(count)} must be greater than 0.");
            }

            for (int i = 0; i < count; i++)
            {
                yield return CreateServiceConnectionCore();
            }
        }

        private IServiceConnection CreateServiceConnectionCore()
        {
            IServiceConnection newConnection;
            lock (_lock)
            {
                ServerConnectionType type;
                int? index = null;

                var count = _serviceConnections.Count;
                if (count < _defaultConnectionCount)
                {
                    type = ServerConnectionType.Default;
                }
                else
                {
                    type = ServerConnectionType.OnDemand;

                    for (int i = 0; i < _defaultConnectionCount; i++)
                    {
                        if (_serviceConnections[i] == null)
                        {
                            type = ServerConnectionType.Default;
                            index = i;
                            break;
                        }
                    }
                }

                newConnection = _serviceConnectionFactory.Create(_connectionFactory, this, type);
                if (index != null)
                {
                    _serviceConnections[index.Value] = newConnection;
                }
                else
                {
                    _serviceConnections.Add(newConnection);
                }
            }

            return newConnection;
        }

        public void DisposeServiceConnection(IServiceConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            lock (_lock)
            {
                int index = _serviceConnections.IndexOf(connection);
                if (index == -1)
                {
                    return;
                }

                if (index >= _defaultConnectionCount)
                {
                    _serviceConnections.RemoveAt(index);
                }
                else if (_serviceConnections.Count > _defaultConnectionCount)
                {
                    _serviceConnections[index] = _serviceConnections[_defaultConnectionCount];
                    _serviceConnections.RemoveAt(_defaultConnectionCount);
                }
                else
                {
                    _serviceConnections[index] = null;
                    _ = KeepDefaultConnectionsAsync();
                }
            }
        }

        private async Task KeepDefaultConnectionsAsync()
        {
            IEnumerable<IServiceConnection> connections = null;
            lock (_lock)
            {
                if (_defaultConnectionReconnectionTime.Add(GetRetryDelay(_defaultConnectionRetry)).Ticks >
                    DateTime.UtcNow.Ticks)
                {
                    _defaultConnectionRetry++;
                    _defaultConnectionReconnectionTime = DateTime.UtcNow;
                    connections = CreateServiceConnection();
                }
            }

            if (connections != null)
            {
                var connection = connections.First();
                await connection.StartAsync();
                if (connection.Status == ServiceConnectionStatus.Connected)
                {
                    Interlocked.Exchange(ref _defaultConnectionRetry, 0);
                }
            }
        }

        public static TimeSpan GetRetryDelay(int retryCount)
        {
            // retry count:   0, 1, 2, 3, 4,  5,  6,  ...
            // delay seconds: 1, 2, 4, 8, 16, 32, 60, ...
            if (retryCount > 5)
            {
                return TimeSpan.FromMinutes(1) + ReconnectInterval;
            }
            return TimeSpan.FromSeconds(1 << retryCount) + ReconnectInterval;
        }

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            return WriteToRandomAvailableConnection(serviceMessage);
        }

        public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            // If we hit this check, it is a code bug.
            if (string.IsNullOrEmpty(partitionKey))
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            return WriteToPartitionedConnection(partitionKey, serviceMessage);
        }

        private Task WriteToPartitionedConnection(string partitionKey, ServiceMessage serviceMessage)
        {
            return WriteWithRetry(serviceMessage, partitionKey.GetHashCode(), _defaultConnectionCount);
        }

        private Task WriteToRandomAvailableConnection(ServiceMessage sm)
        {
            int count = 0;
            Interlocked.Exchange(ref count, _serviceConnections.Count);
            return WriteWithRetry(sm, StaticRandom.Next(count, count), count);
        }

        private async Task WriteWithRetry(ServiceMessage sm, int initial, int maxRetry)
        {
            var retry = 0;
            var direction = initial > 0 ? 1 : - 1;
            while (retry < maxRetry)
            {
                var connection = SelectConnection(_defaultConnectionCount, initial, direction * retry);
                if (connection != null && connection.Status == ServiceConnectionStatus.Connected)
                {
                    try
                    {
                        // still possible the connection is not valid
                        await connection.WriteAsync(sm);
                        return;
                    }
                    catch (ServiceConnectionNotActiveException)
                    {
                        if (retry == maxRetry - 1)
                        {
                            throw;
                        }
                    }
                }
                retry++;
            }
            throw new ServiceConnectionNotActiveException();
        }

        private IServiceConnection SelectConnection(int range, int initial, int step)
        {
            if (range < 0)
            {
                return null;
            }

            int index = (initial & int.MaxValue + step) % range;
            lock (_lock)
            {
                if (index < _serviceConnections.Count)
                {
                    return _serviceConnections[index];
                }

                return null;
            }
        }
    }
}
