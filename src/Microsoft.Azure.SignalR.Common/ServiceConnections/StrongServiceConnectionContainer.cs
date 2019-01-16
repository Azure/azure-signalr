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
        private readonly List<IServiceConnection> _onDemandServiceConnections;

        private readonly int _defaultConnectionCount;

        private readonly IServiceConnectionFactory _serviceConnectionFactory;
        private readonly IConnectionFactory _connectionFactory;

        // The lock is only used to lock the dynamic part (index from _defaultConnectionCount)
        private readonly object _lock = new object();

        private volatile int _defaultConnectionRetry;

        public StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory,
            int defaultConnectionCount)
        {
            _defaultConnectionCount = defaultConnectionCount;
            _serviceConnectionFactory = serviceConnectionFactory;
            _serviceConnections = new List<IServiceConnection>(_defaultConnectionCount);
            _onDemandServiceConnections = new List<IServiceConnection>();
            _connectionFactory = connectionFactory;
        }

        public Task InitializeAsync()
        {
            for (int i = 0; i < _defaultConnectionCount; i++)
            {
                _serviceConnections[i] = _serviceConnectionFactory.Create(_connectionFactory, this, ServerConnectionType.Default);
            }

            return Task.WhenAll(_serviceConnections.Select(c => c.StartAsync()));
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
                newConnection = _serviceConnectionFactory.Create(_connectionFactory, this, ServerConnectionType.OnDemand);
                _onDemandServiceConnections.Add(newConnection);
            }

            return newConnection;
        }

        public void DisposeServiceConnection(IServiceConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            int index = _serviceConnections.IndexOf(connection);
            if (index != -1)
            {
                lock (_lock)
                {
                    if (_onDemandServiceConnections.Count != 0)
                    {
                        _serviceConnections[index] = _onDemandServiceConnections[0];
                        _onDemandServiceConnections.RemoveAt(0);
                        return;
                    }
                }

                _serviceConnections[index] = null;
                _ = ReconnectWithDelayAsync(index);
                return;
            }

            lock (_lock)
            {
                index = _onDemandServiceConnections.IndexOf(connection);
                if (index == -1)
                {
                    return;
                }
                _onDemandServiceConnections.RemoveAt(index);
            }
        }

        private async Task ReconnectWithDelayAsync(int index)
        {
            if (index < 0 || index >= _defaultConnectionCount)
            {
                throw new ArgumentException($"{nameof(index)} must in default connection part");
            }

            await Task.Delay(GetRetryDelay(_defaultConnectionRetry));
            Interlocked.Increment(ref _defaultConnectionRetry);

            var connection = _serviceConnectionFactory.Create(_connectionFactory, this, ServerConnectionType.Default);
            _serviceConnections[index] = connection;

            await connection.StartAsync();
            if (connection.Status == ServiceConnectionStatus.Connected)
            {
                Interlocked.Exchange(ref _defaultConnectionRetry, 0);
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
            return WriteDefaultConnectionWithRetry(serviceMessage, partitionKey.GetHashCode(), _defaultConnectionCount);
        }

        private Task WriteToRandomAvailableConnection(ServiceMessage sm)
        {
            int count = _onDemandServiceConnections.Count;

            var randomIndex = StaticRandom.Next(count + _defaultConnectionCount);
            if (randomIndex < _defaultConnectionCount)
            {
                return WriteDefaultConnectionWithRetry(sm, StaticRandom.Next(-_defaultConnectionCount, _defaultConnectionCount), _defaultConnectionCount);
            }
            else
            {
                return WriteOnDemandConnectionWithRetry(sm, StaticRandom.Next(-count, count), count);
            }
        }

        private async Task WriteDefaultConnectionWithRetry(ServiceMessage sm, int initial, int count)
        {
            // go through all the connections, it can be useful when one of the remote service instances is down
            var maxRetry = count;
            var retry = 0;
            var index = (initial & int.MaxValue) % count;
            var direction = initial > 0 ? 1 : count - 1;
            while (retry < maxRetry)
            {
                var connection = _serviceConnections[index];
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
                index = (index + direction) % count;
            }

            throw new ServiceConnectionNotActiveException();
        }

        private async Task WriteOnDemandConnectionWithRetry(ServiceMessage sm, int initial, int count)
        {
            var maxRetry = count;
            var retry = 0;
            var direction = initial > 0 ? 1 : - 1;
            while (retry < maxRetry)
            {
                var connection = SelectConnection(count, initial, direction * retry);
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
            int index = (initial & int.MaxValue + step) % range;
            lock (_lock)
            {
                if (index < _onDemandServiceConnections.Count)
                {
                    return _onDemandServiceConnections[index];
                }

                return null;
            }
        }
    }
}
