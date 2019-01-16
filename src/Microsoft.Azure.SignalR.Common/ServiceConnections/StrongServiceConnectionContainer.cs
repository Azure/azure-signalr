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
    internal class StrongServiceConnectionContainer : ServiceConnectionContainerBase
    {
        private readonly List<IServiceConnection> _onDemandServiceConnections;

        // The lock is only used to lock the on-demand part
        private readonly object _lock = new object();

        public StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory,
            int count) : base(serviceConnectionFactory, connectionFactory, count)
        {
            _onDemandServiceConnections = new List<IServiceConnection>();
        }

        protected override IServiceConnection GetSingleServiceConnection()
        {
            return GetSingleServiceConnection(ServerConnectionType.Default);
        }

        protected override IServiceConnection CreateServiceConnectionCore()
        {
            IServiceConnection newConnection;

            lock (_lock)
            {
                newConnection = GetSingleServiceConnection(ServerConnectionType.OnDemand);
                _onDemandServiceConnections.Add(newConnection);
            }

            return newConnection;
        }

        public override void DisposeServiceConnection(IServiceConnection connection)
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
                    foreach (var serviceConnection in _onDemandServiceConnections)
                    {
                        if (serviceConnection.Status == ServiceConnectionStatus.Connected)
                        {
                            _serviceConnections[index] = serviceConnection;
                            _onDemandServiceConnections.Remove(serviceConnection);
                            return;
                        }
                    }
                }

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

        protected override Task WriteToRandomAvailableConnection(ServiceMessage serviceMessage)
        {
            int count = _onDemandServiceConnections.Count;

            var randomIndex = StaticRandom.Next(count + _count);
            if (randomIndex < _count)
            {
                return WriteWithRetry(serviceMessage, StaticRandom.Next(-_count, _count), _count);
            }
            else
            {
                return WriteOnDemandConnectionWithRetry(serviceMessage, StaticRandom.Next(-count, count), count);
            }
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
