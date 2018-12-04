// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Transports;
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

        public class TestTransport : IServiceTransport
        {
            public long MessageCount = 0;
            public Func<string, Task> Received { get; set; }
            public Func<Task> Connected { get; set; }
            public Func<Task> Reconnected { get; set; }
            public Func<bool, Task> Disconnected { get; set; }
            public string ConnectionId { get; set; }

            public Task<string> GetGroupsToken()
            {
                return Task.FromResult<string>(null);
            }

            public void OnDisconnected()
            {
            }

            public void OnReceived(string value)
            {
                // Only use to validate message count
                MessageCount++;
            }

            public Task ProcessRequest(ITransportConnection connection)
            {
                return Task.CompletedTask;
            }

            public Task Send(object value)
            {
                return Task.CompletedTask;
            }
        }
    }
}
