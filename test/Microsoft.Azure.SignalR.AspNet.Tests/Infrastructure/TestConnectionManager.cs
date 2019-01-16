// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal class TestConnectionManager : IClientConnectionManager
    {
        public ConcurrentDictionary<string, TestTransport> CurrentTransports = new ConcurrentDictionary<string, TestTransport>();

        public IServiceTransport CreateConnection(OpenConnectionMessage message, IServiceConnection serviceConnection)
        {
            var transport = new TestTransport
            {
                ConnectionId = message.ConnectionId
            };
            CurrentTransports.TryAdd(message.ConnectionId, transport);
            return transport;
        }

        public bool TryGetServiceConnection(string key, out IServiceConnection serviceConnection)
        {
            serviceConnection = null;
            return false;
        }
    }
}
