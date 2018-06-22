// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR.Tests.Infrastructure
{
    class TestClientConnectionManager : IClientConnectionManager
    {
        public void AddClientConnection(ServiceConnectionContext clientConnection)
        {
            return;
        }

        public void RemoveClientConnection(string connectionId)
        {
            return;
        }

        public ConcurrentDictionary<string, ServiceConnectionContext> ClientConnections { get; }
    }
}
