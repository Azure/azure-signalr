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

        // For test purpose only
        internal StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory, List<IServiceConnection> initialConnections) : base(
            serviceConnectionFactory, connectionFactory, initialConnections)
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
            throw new NotImplementedException();
        }

        protected override Task WriteToRandomAvailableConnection(ServiceMessage serviceMessage)
        {
            // The count can't be accurate, but it's enough.
            int onDemandConnectionCount = _onDemandServiceConnections.Count;

            var randomIndex = StaticRandom.Next(onDemandConnectionCount + Count);
            if (randomIndex < Count)
            {
                return WriteWithRetry(serviceMessage, StaticRandom.Next(-Count, Count), Count);
            }
            else
            {
                return WriteOnDemandConnectionWithRetry(serviceMessage, StaticRandom.Next(-onDemandConnectionCount, onDemandConnectionCount), onDemandConnectionCount);
            }
        }

        private async Task WriteOnDemandConnectionWithRetry(ServiceMessage serviceMessage, int initial, int count)
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
            }
            throw new ServiceConnectionNotActiveException();
        }

        private IServiceConnection SelectConnection(int range, int initial, int step)
        {
            // Loop in the range of on-demand connection count got before.
            // The actual count can be changed between loops.
            // Just return null for those have removed.
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
