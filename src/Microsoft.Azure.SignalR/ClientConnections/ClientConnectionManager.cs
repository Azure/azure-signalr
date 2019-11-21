// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionManager : IClientConnectionManager
    {
        private readonly ConcurrentDictionary<string, ClientConnectionContext> _clientConnections =
            new ConcurrentDictionary<string, ClientConnectionContext>();

        public void AddClientConnection(ClientConnectionContext clientConnection)
        {
            _clientConnections[clientConnection.ConnectionId] = clientConnection;
        }

        public ClientConnectionContext RemoveClientConnection(string connectionId)
        {
            _clientConnections.TryRemove(connectionId, out var connection);
            return connection;
        }

        public Task WhenAllCompleted() => Task.WhenAll(_clientConnections.Select(c => c.Value.CompleteTask));

        public IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections => _clientConnections;
    }
}
