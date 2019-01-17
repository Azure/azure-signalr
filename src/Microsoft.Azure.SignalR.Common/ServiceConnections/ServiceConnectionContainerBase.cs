using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    abstract class ServiceConnectionContainerBase : IServiceConnectionContainer, IServiceConnectionManager
    {
        protected readonly IServiceConnectionFactory ServiceConnectionFactory;
        protected readonly IConnectionFactory ConnectionFactory;
        protected readonly List<IServiceConnection> FixedServiceConnections;
        protected readonly int FixedConnectionCount;

        protected ServiceConnectionContainerBase(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory,
            int fixedConnectionCount)
        {
            ServiceConnectionFactory = serviceConnectionFactory;
            ConnectionFactory = connectionFactory;
            FixedServiceConnections = CreateFixedServiceConnection(fixedConnectionCount);
            FixedConnectionCount = fixedConnectionCount;
        }

        protected ServiceConnectionContainerBase(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory, List<IServiceConnection> initialConnections)
        {
            ServiceConnectionFactory = serviceConnectionFactory;
            ConnectionFactory = connectionFactory;
            FixedServiceConnections = initialConnections;
            FixedConnectionCount = initialConnections.Count;
        }

        public virtual Task StartAsync()
        {
            return Task.WhenAll(FixedServiceConnections.Select(c => c.StartAsync()));
        }

        /// <summary>
        /// Get a connection in initialization and reconnection
        /// </summary>
        protected abstract IServiceConnection CreateServiceConnectionCore();

        /// <summary>
        /// Get a connection for a specific service connection type
        /// </summary>
        protected virtual IServiceConnection CreateServiceConnectionCore(ServerConnectionType type)
        {
            return ServiceConnectionFactory.Create(ConnectionFactory, this, type);
        }

        public abstract IServiceConnection CreateServiceConnection();

        public abstract void DisposeServiceConnection(IServiceConnection connection);

        public virtual ServiceConnectionStatus Status => throw new NotSupportedException();

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
            return WriteWithRetry(serviceMessage, partitionKey.GetHashCode(), FixedConnectionCount);
        }

        private Task WriteToRandomAvailableConnection(ServiceMessage serviceMessage)
        {
            return WriteWithRetry(serviceMessage, StaticRandom.Next(-FixedConnectionCount, FixedConnectionCount), FixedConnectionCount);
        }

        private async Task WriteWithRetry(ServiceMessage serviceMessage, int initial, int count)
        {
            // go through all the connections, it can be useful when one of the remote service instances is down
            var maxRetry = count;
            var retry = 0;
            var index = (initial & int.MaxValue) % count;
            var direction = initial > 0 ? 1 : count - 1;
            while (retry < maxRetry)
            {
                var connection = FixedServiceConnections[index];
                if (connection != null && connection.Status == ServiceConnectionStatus.Connected)
                {
                    try
                    {
                        // still possible the connection is not valid
                        await connection.WriteAsync(serviceMessage);
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

        private List<IServiceConnection> CreateFixedServiceConnection(int count)
        {
            var connections = new List<IServiceConnection>();
            for (int i = 0; i < count; i++)
            {
                var connection = CreateServiceConnectionCore();
                connections.Add(connection);
            }

            return connections;
        }
    }
}
