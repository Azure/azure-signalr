// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using Microsoft.AspNet.SignalR;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class TestHub : Hub
    {
        private static ConcurrentDictionary<string, bool> _connectedConnections = new ConcurrentDictionary<string, bool>();
        private static ConcurrentDictionary<string, bool> _connectedUsers = new ConcurrentDictionary<string, bool>();

        public static void ClearConnectedConnectionAndUser()
        {
            _connectedConnections.Clear();
            _connectedUsers.Clear();
        }

        public override Task OnConnected()
        {
            if (!_connectedConnections.TryAdd(Context.ConnectionId, false))
            {
                throw new InvalidOperationException($"Failed to add a client connection {Context.ConnectionId}. Connected connections {string.Join(",", _connectedConnections.Keys)}.");
            }

            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            if (!_connectedConnections.TryRemove(Context.ConnectionId, out _))
            {
                throw new InvalidOperationException($"Failed to remove a client connection {Context.ConnectionId}. Connected connections {string.Join(",", _connectedConnections.Keys)}.");
            }
            return base.OnDisconnected(stopCalled);
        }

        public void Echo(string message)
        {
            Clients.Caller.Echo(message);
        }

        public void Broadcast(string message)
        {
            Clients.All.Broadcast(message);
        }

        public void SendToClient(string message)
        {
            var ind = StaticRandom.Next(0, _connectedConnections.Count);
            Clients.Client(_connectedConnections.Keys.ToList()[ind]).SendToClient(message);
        }

        public void SendToUser(string message)
        {
            var ind = StaticRandom.Next(0, _connectedUsers.Count);
            Clients.User(_connectedUsers.Keys.ToList()[ind]).SendToUser(message);
        }

        public void SendToGroup(string groupName, string message)
        {
            Clients.Group(groupName).SendToGroup(message);
        }

        public Task JoinGroup(string groupName)
        {
            return Groups.Add(Context.ConnectionId, groupName);
        }

        public Task LeaveGroup(string groupName)
        {
            return Groups.Remove(Context.ConnectionId, groupName);
        }
    }
}