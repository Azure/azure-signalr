// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionManager : IClientConnectionManager
    {
        public ClientConnectionManager()
        {
            ClientConnections = new ConcurrentDictionary<string, ServiceConnectionContext>();
        }

        public void AddClientConnection(ServiceConnectionContext clientConnection)
        {
            ClientConnections[clientConnection.ConnectionId] = clientConnection;
        }

        public void RemoveClientConnection(string connectionId)
        {
            ClientConnections.TryRemove(connectionId, out _);
        }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections { get; }
    }
}
