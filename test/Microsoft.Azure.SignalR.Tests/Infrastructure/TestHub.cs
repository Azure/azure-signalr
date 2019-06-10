// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestHub : Hub
    {
        private static ConcurrentDictionary<string, bool> _connectedConnections = new ConcurrentDictionary<string, bool>();
        private static ConcurrentDictionary<string, bool> _connectedUsers = new ConcurrentDictionary<string, bool>();

        public static void ClearConnectedConnectionAndUser()
        {
            _connectedConnections.Clear();
            _connectedUsers.Clear();
        }

        public override Task OnConnectedAsync()
        {
            if (!_connectedConnections.TryAdd(Context.ConnectionId, false))
            {
                throw new InvalidOperationException($"Failed to add a client connection {Context.ConnectionId}. Connected connections {string.Join(",", _connectedConnections.Keys)}.");
            }

            if (!_connectedUsers.TryAdd(Context.UserIdentifier, false))
            {
                throw new InvalidOperationException($"Failed to add connection {Context.ConnectionId} as user {Context.UserIdentifier}. Connected users: {string.Join(", ", _connectedUsers.Keys)}");
            }

            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (!_connectedConnections.TryRemove(Context.ConnectionId, out _))
            {
                throw new InvalidOperationException($"Failed to remove a client connection {Context.ConnectionId}. Connected connections {string.Join(",", _connectedConnections.Keys)}.");
            }

            if (!_connectedUsers.TryRemove(Context.UserIdentifier, out _))
            {
                throw new InvalidOperationException($"Failed to remove a client connection {Context.ConnectionId} for as user {Context.UserIdentifier}. Connected users: {string.Join(", ", _connectedUsers.Keys)}");
            }

            return Task.CompletedTask;
        }

        public void Echo(string message)
        {
            Clients.Caller.SendAsync(nameof(Echo), message);
        }

        public void SendToClient(string message)
        {
            var ind = StaticRandom.Next(0, _connectedConnections.Count);
            Clients.Client(_connectedConnections.Keys.ToList()[ind]).SendAsync(nameof(SendToClient), message);
        }

        public void SendToUser(string message)
        {
            var ind = StaticRandom.Next(0, _connectedUsers.Count);
            Clients.User(_connectedUsers.Keys.ToList()[ind]).SendAsync(nameof(SendToUser), message);
        }

        public void Broadcast(string message)
        {
            Clients.All.SendAsync(nameof(Broadcast), message);
        }

        public Task JoinGroup(string groupName)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public Task LeaveGroup(string groupName)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public void SendToGroup(string groupName, string message)
        {
            Clients.Group(groupName).SendAsync(nameof(SendToGroup), message);
        }
    }
}