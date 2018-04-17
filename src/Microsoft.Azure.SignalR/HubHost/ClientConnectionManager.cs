// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR
{
    public class ClientConnectionManager : IClientConnectionManager
    {
        public ClientConnectionManager()
        {
            ClientConnections = new ConcurrentDictionary<string, ServiceConnectionContext>();
        }

        public string ClientProtocol(string connectionId)
        {
            return ClientConnections[connectionId].ProtocolName;
        }

        public void AddClientConnection(ServiceConnectionContext clientConnection)
        {
            ClientConnections[clientConnection.ConnectionId] = clientConnection;
        }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections { get; }
    }
}
