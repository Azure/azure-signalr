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
        private static readonly int MaxReconnectBackoffInternalInMilliseconds = 1000;
        private static TimeSpan ReconnectInterval =>
            TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackoffInternalInMilliseconds));

        protected readonly IServiceConnectionFactory _serviceConnectionFactory;
        protected readonly IConnectionFactory _connectionFactory;
        protected readonly List<IServiceConnection> _serviceConnections;
        protected readonly int _count;

        private volatile int _defaultConnectionRetry;

        protected ServiceConnectionContainerBase(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory,
            int count)
        {
            _serviceConnectionFactory = serviceConnectionFactory ?? throw new ArgumentNullException(nameof(serviceConnectionFactory));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _serviceConnections = new List<IServiceConnection>(count);
            _count = count;
        }

        public virtual Task InitializeAsync()
        {
            var connections = CreateFixedServiceConnection(_count);
            return Task.WhenAll(connections.Select(c => c.StartAsync()));
        }

        protected abstract IServiceConnection GetSingleServiceConnection();

        protected virtual IServiceConnection GetSingleServiceConnection(ServerConnectionType type)
        {
            return _serviceConnectionFactory.Create(_connectionFactory, this, type);
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

        protected abstract IServiceConnection CreateServiceConnectionCore();

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

        protected virtual Task WriteToPartitionedConnection(string partitionKey, ServiceMessage serviceMessage)
        {
            return WriteWithRetry(serviceMessage, partitionKey.GetHashCode(), _count);
        }

        protected virtual Task WriteToRandomAvailableConnection(ServiceMessage serviceMessage)
        {
            return WriteWithRetry(serviceMessage, StaticRandom.Next(-_count, _count), _count);
        }

        protected virtual async Task WriteWithRetry(ServiceMessage serviceMessage, int initial, int count)
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

        protected virtual async Task ReconnectWithDelayAsync(int index)
        {
            if (index < 0 || index >= _count)
            {
                throw new ArgumentException($"{nameof(index)} must bewteen 0 and {_count}");
            }

            await Task.Delay(GetRetryDelay(_defaultConnectionRetry));
            Interlocked.Increment(ref _defaultConnectionRetry);

            var connection = GetSingleServiceConnection();
            _serviceConnections[index] = connection;

            await connection.StartAsync();
            if (connection.Status == ServiceConnectionStatus.Connected)
            {
                Interlocked.Exchange(ref _defaultConnectionRetry, 0);
            }
        }

        private TimeSpan GetRetryDelay(int retryCount)
        {
            // retry count:   0, 1, 2, 3, 4,  5,  6,  ...
            // delay seconds: 1, 2, 4, 8, 16, 32, 60, ...
            if (retryCount > 5)
            {
                return TimeSpan.FromMinutes(1) + ReconnectInterval;
            }
            return TimeSpan.FromSeconds(1 << retryCount) + ReconnectInterval;
        }

        private IEnumerable<IServiceConnection> CreateFixedServiceConnection(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var connection = GetSingleServiceConnection();
                _serviceConnections[i] = connection;
                yield return connection;
            }
        }
    }
}
