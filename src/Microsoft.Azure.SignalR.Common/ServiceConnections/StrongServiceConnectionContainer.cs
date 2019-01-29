// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class StrongServiceConnectionContainer : ServiceConnectionContainerBase
    {
        private readonly List<IServiceConnection> _onDemandServiceConnections;

        // The lock is only used to lock the on-demand part
        private readonly object _lock = new object();

        public StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory,
            int fixedConnectionCount, ServiceEndpoint endpoint) : base(serviceConnectionFactory, connectionFactory, fixedConnectionCount, endpoint)
        {
            _onDemandServiceConnections = new List<IServiceConnection>();
        }

        // For test purpose only
        internal StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory, List<IServiceConnection> initialConnections, ServiceEndpoint endpoint) : base(
            serviceConnectionFactory, connectionFactory, initialConnections, endpoint)
        {
            _onDemandServiceConnections = new List<IServiceConnection>();
        }

        protected override ServiceConnectionStatus GetStatus()
        {
            var status = base.GetStatus();
            if (status == ServiceConnectionStatus.Connected)
            {
                return status;
            }

            lock (_lock)
            {
                if (_onDemandServiceConnections.Any(s => s.Status == ServiceConnectionStatus.Connected))
                {
                    return ServiceConnectionStatus.Connected;
                }
            }

            return ServiceConnectionStatus.Disconnected;
        }

        protected override IServiceConnection CreateServiceConnectionCore()
        {
            return CreateServiceConnectionCore(ServerConnectionType.Default);
        }

        public override IServiceConnection CreateServiceConnection()
        {
            IServiceConnection newConnection;

            lock (_lock)
            {
                newConnection = CreateServiceConnectionCore(ServerConnectionType.OnDemand);
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

            int index = FixedServiceConnections.IndexOf(connection);
            if (index != -1)
            {
                lock (_lock)
                {
                    foreach (var serviceConnection in _onDemandServiceConnections)
                    {
                        if (serviceConnection.Status == ServiceConnectionStatus.Connected)
                        {
                            FixedServiceConnections[index] = serviceConnection;
                            _onDemandServiceConnections.Remove(serviceConnection);
                            return;
                        }
                    }
                }

                var task = RestartServiceConnectionCoreAsync(index);
                if (task.Exception != null)
                {
                    throw task.Exception;
                }

                return;
            }

            lock (_lock)
            {
                index = _onDemandServiceConnections.IndexOf(connection);
                if (index != -1)
                {
                    _onDemandServiceConnections.RemoveAt(index);
                }
            }
        }
    }
}
