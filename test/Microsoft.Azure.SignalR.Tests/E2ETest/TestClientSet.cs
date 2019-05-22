// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Tests.Common;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestClientSet : ITestClientSet
    {
        private readonly IList<HubConnection> _connections;

        public int Count => _connections?.Count ?? 0;

        public TestClientSet(string serverUrl, int count)
        {
            if (serverUrl == null)
            {
                throw new ArgumentNullException(nameof(serverUrl));
            }

            _connections = (from i in Enumerable.Range(0, count)
                            select new HubConnectionBuilder().WithUrl($"{serverUrl}/{nameof(TestHub)}").Build()).ToList();
        }

        public Task StartAsync()
        {
            return Task.WhenAll(from conn in _connections
                                select conn.StartAsync());
        }

        public Task StopAsync()
        {
            return Task.WhenAll(from conn in _connections
                                select conn.StopAsync());
        }

        public void AddListener(string methodName, Action<string> handler)
        {
            foreach (var conn in _connections)
            {
                conn.On(methodName, handler);
            }
        }

        public Task SendAsync(string methodName, int sendCount, params string [] messages)
        {
            return Task.WhenAll(_connections.Select((conn, i) =>
            {
                if (sendCount == -1 || i < sendCount)
                {
                    return conn.SendAsync(methodName, messages);
                }
                return Task.CompletedTask;
            }));
        }

        public Task SendAsync(string methodName, IList<int> connIndList, params string[] messages)
        {

        }

        public Task ManageGroupAsync(string methodName, IDictionary<int, string> connectionGroupMap)
        {
            return Task.WhenAll(from entry in connectionGroupMap
                                let connInd = entry.Key
                                let groupName = entry.Value
                                where connectionGroupMap.ContainsKey(connInd)
                                select _connections[connInd].SendAsync(methodName, groupName));
        }
    }
}