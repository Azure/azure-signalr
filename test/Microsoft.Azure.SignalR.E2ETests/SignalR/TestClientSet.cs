// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestClientSet : ITestClientSet
    {
        private readonly IList<HubConnection> _connections;
        private ITestOutputHelper _output;

        public int Count => _connections?.Count ?? 0;

        public TestClientSet(string serverUrl, int count, ITestOutputHelper output)
        {
            _output = output;
            if (serverUrl == null)
            {
                throw new ArgumentNullException(nameof(serverUrl));
            }

            _connections = (from i in Enumerable.Range(0, count)
                            select new HubConnectionBuilder().WithUrl($"{serverUrl}/{nameof(TestHub)}?user=user_{i}").Build()).ToList();

            foreach (var conn in _connections)
            {
                conn.Closed += ex =>
                {
                    if (ex != null)
                    {
                        _output.WriteLine($"Client connection closed: {ex}");
                    }
                    return Task.CompletedTask;
                };
            }
        }

        public Task StartAsync()
        {
            return Task.WhenAll(from conn in _connections select conn.StartAsync());
        }

        public Task StopAsync()
        {
            return Task.WhenAll(from conn in _connections select conn.StopAsync());
        }

        public void AddListener(string methodName, Action<string> handler)
        {
            foreach (var conn in _connections)
            {
                conn.On(methodName, handler);
            }
        }

        public Task SendAsync(string methodName, int sendCount, params string[] messages)
        {
            return Task.WhenAll(_connections
                .Where((_, i) => sendCount == -1 || i < sendCount)
                .Select(conn => conn.SendCoreAsync(methodName, messages)));
        }

        public Task SendAsync(string methodName, int[] sendInds, params string[] messages)
        {
            return Task.WhenAll(_connections
                .Where((_, i) => sendInds.Contains(i))
                .Select(conn => conn.SendCoreAsync(methodName, messages)));
        }

        public Task ManageGroupAsync(string methodName, IDictionary<int, string> connectionGroupMap)
        {
            return Task.WhenAll(from entry in connectionGroupMap
                                let connInd = entry.Key
                                let groupName = entry.Value
                                select _connections[connInd].SendAsync(methodName, groupName));
        }
    }
}