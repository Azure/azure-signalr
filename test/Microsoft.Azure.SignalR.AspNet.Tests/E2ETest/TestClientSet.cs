// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.Azure.SignalR.Tests.Common;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class TestClientSet : ITestClientSet
    {
        private IList<HubConnection> _connections;
        private IList<IHubProxy> _proxies;

        public int Count
        {
            get => _connections?.Count ?? 0;
        }

        public TestClientSet(string serverUrl, int count)
        {
            _connections = (from i in Enumerable.Range(0, count)
                            select new HubConnection(serverUrl)).ToList();

            _proxies = (from conn in _connections
                        select conn.CreateHubProxy(nameof(TestHub))).ToList();
        }

        public void AddListener(string methodName, Action<string> handler)
        {
            foreach (var p in _proxies)
            {
                p.On(methodName, handler);
            }
        }

        public Task AllSendAsync(string methodName, string message)
        {
            return Task.WhenAll(from p in _proxies
                                select p.Invoke(methodName, message));
        }

        public Task StartAsync()
        {
            return Task.WhenAll(from conn in _connections
                                select conn.Start());
        }

        public Task StopAsync()
        {
            foreach (var conn in _connections)
            {
                conn.Stop();
            }

            return Task.CompletedTask;
        }
    }
}