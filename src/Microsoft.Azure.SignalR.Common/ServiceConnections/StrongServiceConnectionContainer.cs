// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class StrongServiceConnectionContainer : ServiceConnectionContainerBase
    {
        private const string PingTargetKey = "target";
        private readonly List<IServiceConnection> _onDemandServiceConnections;

        // The lock is only used to lock the on-demand part
        private readonly object _lock = new object();

        public StrongServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            int fixedConnectionCount, HubServiceEndpoint endpoint, ILogger logger = null) : base(serviceConnectionFactory, fixedConnectionCount, endpoint, logger: logger)
        {
            _onDemandServiceConnections = new List<IServiceConnection>();
        }

        public override Task HandlePingAsync(PingMessage pingMessage)
        {
            int index = 0;
            while (index < pingMessage.Messages.Length - 1)
            {
                if (pingMessage.Messages[index] == PingTargetKey &&
                    !string.IsNullOrEmpty(pingMessage.Messages[index + 1]))
                {
                    var connection = CreateOnDemandServiceConnection();
                    return StartCoreAsync(connection, pingMessage.Messages[index + 1]);
                }

                index += 2;
            }

            return Task.CompletedTask;
        }

        public override Task StopAsync()
        {
            var stopOndemandConnectionsTask =  Task.WhenAll(_onDemandServiceConnections.Select(c => c.StopAsync()));
            return Task.WhenAll(stopOndemandConnectionsTask, base.StopAsync());
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

        protected override async Task OnConnectionComplete(IServiceConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            int index;
            lock (_lock)
            {
                index = _onDemandServiceConnections.IndexOf(connection);
                if (index != -1)
                {
                    _onDemandServiceConnections.RemoveAt(index);
                    return;
                }
            }

            index = FixedServiceConnections.IndexOf(connection);
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
                            ReplaceFixedConnections(index, serviceConnection);
                            _onDemandServiceConnections.Remove(serviceConnection);
                            return;
                        }
                    }
                }

                // Restart a default connection.
                await base.OnConnectionComplete(connection);
            }
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
    }
}
