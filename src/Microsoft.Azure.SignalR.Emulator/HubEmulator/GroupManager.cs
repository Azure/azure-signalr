// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal class GroupManager
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly ManyToManyMap<string, string> _connectionGroupMap =
            new ManyToManyMap<string, string>(StringComparer.Ordinal, StringComparer.Ordinal);
        private readonly ManyToManyMap<string, string> _userGroupMap =
            new ManyToManyMap<string, string>(StringComparer.Ordinal, StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Connections> _userConnections =
            new ConcurrentDictionary<string, Connections>(StringComparer.Ordinal);
        private readonly Dictionary<(string user, string group), DateTimeOffset> _expires =
            new Dictionary<(string user, string group), DateTimeOffset>();

        public void AddConnectionIntoGroup(string connectionId, string group)
        {
            _connectionGroupMap.Add(connectionId, group);
        }

        public bool RemoveConnectionFromGroup(string connectionId, string group)
        {
            return _connectionGroupMap.Remove(connectionId, group);
        }

        public void AddUserToGroup(string user, string group, DateTimeOffset expireAt)
        {
            _userGroupMap.Add(user, group);
            lock (_expires)
            {
                _expires[(user, group)] = expireAt;
            }
            _ = CleanExpiresAsync();
            var conns = _userConnections.GetOrAdd(user, _ => new Connections());
            lock (conns)
            {
                foreach (var conn in conns)
                {
                    _connectionGroupMap.Add(conn.ConnectionId, group);
                }
            }
        }

        public void RemoveUserFromGroup(string user, string group)
        {
            _userGroupMap.Remove(user, group);
            lock (_expires)
            {
                _expires.Remove((user, group));
            }
            if (_userConnections.TryGetValue(user, out var conns))
            {
                lock (conns)
                {
                    foreach (var conn in conns)
                    {
                        _connectionGroupMap.Remove(conn.ConnectionId, group);
                    }
                }
            }
        }

        public void RemoveUserFromAllGroups(string user)
        {
            _userGroupMap.RemoveLeft(user);
            lock (_expires)
            {
                var expToRemove = (from exp in _expires
                                   where exp.Key.user == user
                                   select exp.Key).ToList();
                foreach (var exp in expToRemove)
                {
                    _expires.Remove(exp);
                }
            }
            if (_userConnections.TryGetValue(user, out var conns))
            {
                lock (conns)
                {
                    foreach (var conn in conns)
                    {
                        _connectionGroupMap.RemoveLeft(conn.ConnectionId);
                    }
                }
            }
        }

        public void OnConnectionClosing(HubConnectionContext connection)
        {
            using (var groups = _connectionGroupMap.QueryByLeft(connection.ConnectionId))
            {
                foreach (var g in groups.Value)
                {
                    _connectionGroupMap.Remove(connection.ConnectionId, g);
                }
            }
            if (connection.UserIdentifier != null &&
                _userConnections.TryGetValue(connection.UserIdentifier, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(connection);
                    if (connections.Count == 0)
                    {
                        ((ICollection<KeyValuePair<string, Connections>>)_userConnections).Remove(new KeyValuePair<string, Connections>(connection.UserIdentifier, connections));
                    }
                }
            }
        }

        public void OnConnectionOpenning(HubConnectionContext connection)
        {
            if (connection.UserIdentifier != null)
            {
                _userConnections.AddOrUpdate(
                    connection.UserIdentifier,
                    new Connections { connection },
                    (_, connections) =>
                    {
                        lock (connections)
                        {
                            connections.Add(connection);
                        }
                        return connections;
                    });
                using (var groups = _userGroupMap.QueryByLeft(connection.UserIdentifier))
                {
                    foreach (var g in groups.Value)
                    {
                        _connectionGroupMap.Add(connection.ConnectionId, g);
                    }
                }
            }
        }

        public bool GroupContainsConnections(string group) => _connectionGroupMap.RightExists(group);

        public LeaseForArray<string> GetConnectionsForGroup(string group) =>
            _connectionGroupMap.QueryByRight(group);

        public LeaseForArray<HubConnectionContext> GetConnectionsForUser(string user)
        {
            if (!_userConnections.TryGetValue(user, out var set))
            {
                return LeaseForArray<HubConnectionContext>.Empty;
            }
            lock (set)
            {
                var array = ArrayPool<HubConnectionContext>.Shared.Rent(set.Count);
                set.CopyTo(array);
                return LeaseForArray.Create(array, set.Count);
            }
        }

        public LeaseForArray<string> GetGroupsForUser(string user) =>
            _userGroupMap.QueryByLeft(user);

        public LeaseForArray<string> GetUsersInGroup(string group) =>
            _userGroupMap.QueryByRight(group);

        public bool GroupContainsUser(string group, string user) => _userGroupMap.Exists(group, user);

        private async Task CleanExpiresAsync()
        {
            if (!_semaphore.Wait(0))
            {
                return;
            }
            try
            {
                await CleanExpiresCoreAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task CleanExpiresCoreAsync()
        {
            while (true)
            {
                await Task.Delay(1000);
                List<(string user, string group)> list = null;
                lock (_expires)
                {
                    if (_expires.Count == 0)
                    {
                        return;
                    }
                    var now = DateTimeOffset.Now;
                    foreach (var pair in _expires)
                    {
                        if (pair.Value < now)
                        {
                            if (list == null)
                            {
                                list = new List<(string user, string group)> { pair.Key };
                            }
                            else
                            {
                                list.Add(pair.Key);
                            }
                        }
                    }
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            _expires.Remove(item);
                            _userGroupMap.Remove(item.user, item.group);
                        }
                    }
                }
            }
        }

        private sealed class Connections : HashSet<HubConnectionContext>
        {
            public override int GetHashCode() => Count;

            public override bool Equals(object obj)
            {
                return Count == (obj as Connections)?.Count;
            }
        }
    }
}
