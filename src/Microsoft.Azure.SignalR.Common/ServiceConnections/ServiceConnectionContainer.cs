// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnectionContainer : IServiceConnectionContainer
    {
        private const int RetryCount = 10;
        private readonly List<IServiceConnection> _defaultServiceConnections;
        private readonly ConcurrentDictionary<IServiceConnection, int> _onDemandServiceConnections = new ConcurrentDictionary<IServiceConnection, int>();
        private readonly int _defaultConnectionCount;

        public ServiceConnectionContainer(List<IServiceConnection> defaultServiceConnections)
        {
            _defaultServiceConnections = defaultServiceConnections;
            _defaultConnectionCount = _defaultServiceConnections.Count;
        }

        public ServiceConnectionContainer(Func<IServiceConnectionContainer, IServiceConnection> generator, int defaultConnectionCount)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            if (defaultConnectionCount <= 0)
            {
                throw new ArgumentException($"{nameof(defaultConnectionCount)} must be greater than 0.");
            }

            _defaultServiceConnections = new List<IServiceConnection>(defaultConnectionCount);
            for (int i = 0; i < defaultConnectionCount; i++)
            {
                _defaultServiceConnections.Add(generator(this));
            }

            _defaultConnectionCount = defaultConnectionCount;
        }

        // All the on-demand connections will be added seperately. Messages that need order will not be send to
        // on-demand connections by default.
        public void AddServiceConnection(IServiceConnection serviceConnection)
        {
            if (serviceConnection == null)
            {
                throw new ArgumentNullException(nameof(serviceConnection));
            }

            _onDemandServiceConnections.AddOrUpdate(serviceConnection, 0, (_, __) => 0);
        }

        public void RemoveServiceConnection(IServiceConnection serviceConnection)
        {
            if (serviceConnection == null)
            {
                throw new ArgumentNullException(nameof(serviceConnection));
            }

            _onDemandServiceConnections.TryRemove(serviceConnection, out _);
        }

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public Task StartAsync()
        {
            return Task.WhenAll(_defaultServiceConnections.Select(c => c.StartAsync()));
        }

        public Task StopAsync()
        {
            return Task.WhenAll(_defaultServiceConnections.Select(c => c.StopAsync()));
        }

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
            return WriteWithRetry(serviceMessage, partitionKey.GetHashCode());
        }

        private Task WriteToRandomAvailableConnection(ServiceMessage sm)
        {
            int totalWeight = _defaultConnectionCount + _onDemandServiceConnections.Count;
            int random = StaticRandom.Next(0, totalWeight);
            if (random < _defaultConnectionCount)
            {
                return WriteWithRetry(sm, random);
            }

            return WriteToOnDemandConnectionWithRetry(sm, random);
        }

        private async Task WriteWithRetry(ServiceMessage sm, int initial)
        {
            var retry = 0;
            var index = (initial & int.MaxValue) % _defaultConnectionCount;
            var direction = initial > 0 ? 1 : _defaultConnectionCount - 1;
            var maxRetry = _defaultConnectionCount;
            while (retry < maxRetry)
            {
                var connection = _defaultServiceConnections[index];
                if (await WriteCore(connection, sm))
                {
                    return;
                }

                retry++;
                index = (index + direction) % _defaultConnectionCount;
            }

            // Fallback to on-demand connections
            await WriteToOnDemandConnectionWithRetry(sm, initial);
        }

        private async Task WriteToOnDemandConnectionWithRetry(ServiceMessage sm, int initial)
        {
            var retry = 0;
            var maxRetry = _onDemandServiceConnections.Count;
            if (maxRetry == 0)
            {
                throw new ServiceConnectionNotActiveException();
            }
            int index = (initial & int.MaxValue) % maxRetry;
            var direction = initial > 0 ? 1 : maxRetry - 1;
            while (retry < maxRetry)
            {
                var connection = _onDemandServiceConnections.ElementAtOrDefault(index).Key;
                if (connection != null && await WriteCore(connection, sm))
                {
                    return;
                }

                retry++;
                index = (index + direction) % maxRetry;
            }

            throw new ServiceConnectionNotActiveException();
        }

        private async Task<bool> WriteCore(IServiceConnection connection, ServiceMessage sm)
        {
            if (connection != null && connection.Status == ServiceConnectionStatus.Connected)
            {
                try
                {
                    // still possible the connection is not valid
                    await connection.WriteAsync(sm);
                    return true;
                }
                catch (ServiceConnectionNotActiveException)
                {
                }
            }

            return false;
        }
    }
}
