// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR
{
    public interface IClientConnectionManager
    {
        void AddClientConnection(ServiceConnectionContext clientConnection);

        ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections { get; }

        string ClientProtocol(string connectionId);
    }
}
