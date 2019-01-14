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
    internal class ServiceConnectionContainer : IServiceConnectionContainer
    {
        private const int RetryCount = 10;
        private readonly List<IServiceConnection> _serviceConnections;
        private readonly int _count;
        private readonly IServiceConnectionFactory _connectionFactory;
        private readonly int _defaultConnectionCount;
        private readonly object _lock = new object();


        public ServiceConnectionContainer(List<IServiceConnection> serviceConnections)
        {
            _serviceConnections = serviceConnections;
            _count = _serviceConnections.Count;
        }

        public ServiceConnectionContainer(Func<IServiceConnection> generator, int count)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            if (count <= 0)
            {
                throw new ArgumentException($"{nameof(count)} must be greater than 0.");
            }

            _serviceConnections = new List<IServiceConnection>(count);
            for (int i = 0; i < count; i++)
            {
                _serviceConnections.Add(generator());
            }

            _count = count;
        }

        public IEnumerable<IServiceConnection> CreateServiceConnectionAsync(int count = 1)
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
                var count = _serviceConnections.Count;
                if (count < _defaultConnectionCount)
                {
                    type = ServerConnectionType.Default;
                }
                else
                {
                    type = ServerConnectionType.OnDemand;
                }

                newConnection = _connectionFactory.Create(type);
                _serviceConnections.Add(newConnection);
            }

            return newConnection;
        }

        public void DisposeServiceConnectionAsync(IServiceConnection connection)
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
                }
            }

            _ = KeepDefaultConnectionsAsync();
        }

        private async Task KeepDefaultConnectionsAsync()
        {

        }

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public Task StartAsync()
        {
            return Task.WhenAll(_serviceConnections.Select(c => c.StartAsync()));
        }

        public Task StopAsync()
        {
            return Task.WhenAll(_serviceConnections.Select(c => c.StopAsync()));
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
            return WriteWithRetry(sm, StaticRandom.Next(-_defaultConnectionCount, _defaultConnectionCount));
        }

        private async Task WriteWithRetry(ServiceMessage sm, int initial)
        {
            //var retry = 0;
            //var index = (initial & int.MaxValue) % _defaultConnectionCount;
            //var direction = initial > 0 ? 1 : _defaultConnectionCount - 1;
            //var maxRetry = _defaultConnectionCount;
            //while (retry < maxRetry)
            //{
            //    var connection = _serviceConnections[index];
            //    if (connection != null && connection.Status == ServiceConnectionStatus.Connected)
            //    {
            //        try
            //        {
            //            // still possible the connection is not valid
            //            await connection.WriteAsync(sm);
            //            return;
            //        }
            //        catch (ServiceConnectionNotActiveException)
            //        {
            //            if (retry == maxRetry - 1)
            //            {
            //                throw;
            //            }
            //        }
            //    }

            //    retry++;
            //    index = (index + direction) % _defaultConnectionCount;
            //}

            //throw new ServiceConnectionNotActiveException();
            var maxRetry = _defaultConnectionCount;
            var retry = 0;
            while (retry < maxRetry)
            {
                var connection = SelectConnection();
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

        private IServiceConnection SelectConnection()
        {

        }
    }
}
