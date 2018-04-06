// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR
{
    public class ConnectionManager : IConnectionManager
    {
        private readonly List<ServiceConnection> _serviceConnections = new List<ServiceConnection>();            

        public ConnectionManager()
        {
            ClientConnections = new ConcurrentDictionary<string, ServiceConnectionContext>();
        }

        public TransferFormat ClientTransferFormat(string connectionId)
        {
            return ClientConnections[connectionId].TransferFormat;
        }

        public void AddClientConnection(ServiceConnectionContext clientConnection)
        {
            ClientConnections[clientConnection.ConnectionId] = clientConnection;
        }

        public void AddServiceConnection(ServiceConnection serviceConnection)
        {
            _serviceConnections.Add(serviceConnection);
        }

        public async Task StartAllServiceConnection(ConnectionDelegate connectionDelegate)
        {
            var tasks = _serviceConnections.Select(c => c.StartAsync(connectionDelegate));
            await Task.WhenAll(tasks);
        }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections { get; }

        public Task SendServiceMessage(ServiceMessage serviceMessage)
        {
            var index = StaticRandom.Next(_serviceConnections.Count);
            _ = _serviceConnections[index].SendServiceMessage(serviceMessage);
            return Task.CompletedTask;
        }
    }
}
