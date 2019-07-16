// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class TestHubConnectionManager
    {
        public int ClientCount => _connectedConnections.Count;

        public int UserCount => _connectedUsers.Count;

        public IList<string> Clients => _connectedConnections.Keys.ToList();

        public IList<string> Users => _connectedUsers.Keys.ToList();

        private ConcurrentDictionary<string, bool> _connectedConnections = new ConcurrentDictionary<string, bool>();
        private ConcurrentDictionary<string, bool> _connectedUsers = new ConcurrentDictionary<string, bool>();

        private ConcurrentDictionary<int, TaskCompletionSource<string>> _connectionCountTcs =
            new ConcurrentDictionary<int, TaskCompletionSource<string>>();

        private int _count = 0;

        public void ClearAll()
        {
            _connectedConnections.Clear();
            _connectedUsers.Clear();
            _connectionCountTcs.Clear();
        }

        public void AddClient(string client)
        {
            if (!_connectedConnections.TryAdd(client, false))
            {
                throw new InvalidOperationException($"Failed to add client connection {client}. Connected connections {string.Join(",", _connectedConnections.Keys)}.");
            }

            var count = Interlocked.Increment(ref _count);
            if (_connectionCountTcs.TryGetValue(count, out var tcs))
            {
                tcs.TrySetResult(client);
            }
        }

        public void AddUser(string user)
        {
            if (!_connectedUsers.TryAdd(user, false))
            {
                throw new InvalidOperationException($"Failed to add user {user}. Connected users: {string.Join(", ", _connectedUsers.Keys)}");
            }
        }

        public void RemoveClient(string client)
        {
            if (!_connectedConnections.TryRemove(client, out _))
            {
                throw new InvalidOperationException($"Failed to remove client connection {client}. Connected connections {string.Join(",", _connectedConnections.Keys)}.");
            }
        }

        public void RemoveUser(string user)
        {
            if (!_connectedUsers.TryRemove(user, out _))
            {
                throw new InvalidOperationException($"Failed to remove user {user}. Connected users: {string.Join(", ", _connectedUsers.Keys)}");
            }
        }

        public Task<string> WaitForConnectionCountAsync(int n)
        {
            return _connectionCountTcs.GetOrAdd(n,
                k => new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }
    }
}
