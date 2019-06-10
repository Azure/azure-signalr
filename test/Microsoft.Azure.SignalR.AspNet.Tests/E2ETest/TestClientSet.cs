// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class TestClientSet : ITestClientSet
    {
        private readonly IList<HubConnection> _connections;
        private readonly IList<IHubProxy> _proxies;
        private readonly ITestOutputHelper _output;

        public int Count => _connections?.Count ?? 0;

        public TestClientSet(string serverUrl, int count, ITestOutputHelper output)
        {
            _connections = (from i in Enumerable.Range(0, count)
                            select new HubConnection(serverUrl, $"user=user_{i}")).ToList();

            foreach (var conn in _connections)
            {
                conn.Closed += () => _output.WriteLine($"Client connection closed.");
                conn.Error += ex => _output.WriteLine($"Client error: {ex}");
            }

            _proxies = (from conn in _connections
                        select conn.CreateHubProxy(nameof(TestHub))).ToList();

            _output = output;
        }

        public void AddListener(string methodName, Action<string> handler)
        {
            foreach (var p in _proxies)
            {
                p.On(methodName, handler);
            }
        }

        public Task SendAsync(string methodName, int sendCount, params string[] messages)
        {
            return Task.WhenAll(_proxies
                .Where((_, i) => sendCount == -1 || i < sendCount)
                .Select(p => p.Invoke(methodName, messages)));
        }

        public Task SendAsync(string methodName, int[] sendInds, params string[] messages)
        {
            return Task.WhenAll(_proxies
                .Where((_, i) => sendInds.Contains(i))
                .Select(p => p.Invoke(methodName, messages)));
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

        public Task ManageGroupAsync(string methodName, IDictionary<int, string> connectionGroupMap)
        {
            return Task.WhenAll(from entry in connectionGroupMap
                                let proxyInd = entry.Key
                                let groupName = entry.Value
                                select _proxies[proxyInd].Invoke(methodName, groupName));
        }
    }
}