// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public override async Task HandlePingAsync(string target)
        {
            var connection = CreateOnDemandServiceConnection();
            await StartCoreAsync(connection, target);
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

        private IServiceConnection CreateOnDemandServiceConnection()
        {
            IServiceConnection newConnection;

            lock (_lock)
            {
                newConnection = CreateServiceConnectionCore(ServerConnectionType.OnDemand);
                _onDemandServiceConnections.Add(newConnection);
            }

            return newConnection;
        }

        protected override async Task DisposeOrRestartServiceConnectionAsync(IServiceConnection connection)
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
                        // We have a connected on-demand connection,
                        // then promote it to default connection.
                        if (serviceConnection.Status == ServiceConnectionStatus.Connected)
                        {
                            FixedServiceConnections[index] = serviceConnection;
                            _onDemandServiceConnections.Remove(serviceConnection);
                            return;
                        }
                    }
                }

                // Restart a default connection.
                await RestartServiceConnectionCoreAsync(index);
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
