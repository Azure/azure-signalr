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
        private IList<HubConnection> _connections = new List<HubConnection>();

        public int Count
        {
            get => _connections?.Count ?? 0;
        }

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

        public Task AllSendAsync(string methodName, string message)
        {
            return Task.WhenAll(from conn in _connections
                                select conn.SendAsync(methodName, message));
        }
    }
}