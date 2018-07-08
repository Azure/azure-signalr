// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.Azure.SignalR.Tests.Infrastructure
{
    class TestClientConnectionManager : IClientConnectionManager
    {
        public void AddClientConnection(ServiceConnectionContext clientConnection)
        {
        }

        public void RemoveClientConnection(string connectionId)
        {
        }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections { get; }
    }
}
