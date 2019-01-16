using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Common.ServiceConnections
{
    class WeakServiceConnectionContainer : IServiceConnectionContainer
    {
        private readonly IServiceConnectionFactory _serviceConnectionFactory;
        private readonly IConnectionFactory _connectionFactory;
        private readonly List<IServiceConnection> _serviceConnections;
        private readonly int _count;

        public WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory, 
            IConnectionFactory connectionFactory, 
            int count)
        {
            _serviceConnectionFactory = serviceConnectionFactory ?? throw new ArgumentNullException(nameof(serviceConnectionFactory));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _serviceConnections = new List<IServiceConnection>(count);
            _count = count;
        }

        public Task InitializeAsync()
        {
            var connections = CreateServiceConnection(_count);
            return Task.WhenAll(connections.Select(c => c.StartAsync()));
        }

        private IEnumerable<IServiceConnection> CreateServiceConnection(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var connection = _serviceConnectionFactory.Create(_connectionFactory, this, ServerConnectionType.Weak);
                _serviceConnections[i] = connection;
                yield return connection;
            }
        }

        public void DisposeServiceConnection(IServiceConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            int index = _serviceConnections.IndexOf(connection);
            if (index == -1)
            {
                return;
            }

            _serviceConnections[index] = _serviceConnectionFactory.Create(_connectionFactory, this, ServerConnectionType.Weak);
            _ = _serviceConnections[index].StartAsync();
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
            return WriteWithRetry(serviceMessage, partitionKey.GetHashCode(), _count);
        }

        private Task WriteToRandomAvailableConnection(ServiceMessage sm)
        {
            return WriteWithRetry(sm, StaticRandom.Next(-_count, _count), _count);
        }

        private async Task WriteWithRetry(ServiceMessage sm, int initial, int count)
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
    }
}
