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

        public TransferFormat ClientTransferFormat(string connectionId)
        {
            return ClientConnections[connectionId].TransferFormat;
        }

        public void AddClientConnection(ServiceConnectionContext clientConnection)
        {
            ClientConnections[clientConnection.ConnectionId] = clientConnection;
        }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections { get; }
    }
}
