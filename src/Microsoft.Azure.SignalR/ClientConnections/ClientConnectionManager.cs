// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionManager : IClientConnectionManager
    {
        private  readonly ConcurrentDictionary<string, ServiceConnectionContext> _clientConnections = new ConcurrentDictionary<string, ServiceConnectionContext>();

        public ClientConnectionManager()
        {
        }

        public void AddClientConnection(ServiceConnectionContext clientConnection)
        {
            _clientConnections[clientConnection.ConnectionId] = clientConnection;
        }

        public ServiceConnectionContext RemoveClientConnection(string connectionId)
        {
            _clientConnections.TryRemove(connectionId, out var connection);
            return connection;
        }

        public IReadOnlyDictionary<string, ServiceConnectionContext> ClientConnections => _clientConnections;
    }
}
