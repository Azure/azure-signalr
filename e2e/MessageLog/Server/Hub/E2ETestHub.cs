// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.E2ETest
{
    public class E2ETestHub : Hub
    {
        private Data _data;
        private Random _rand = new Random();

        public E2ETestHub(Data data)
        {
            _data = data;
        }

        public override Task OnConnectedAsync()
        {
            _data.Connections.AddOrUpdate(Context.ConnectionId, 0, (k, v) => 0);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            _data.Connections.Remove(Context.ConnectionId, out _);
            return base.OnDisconnectedAsync(exception);
        }

        public void Broadcast(string message)
        {
            Clients.All.SendAsync("broadcast", message);
        }

        public void Echo(string message)
        {
            Clients.Client(Context.ConnectionId).SendAsync("echo", message);
        }

        public void SendToClientRandomly(string message)
        {
            var connections = (from kvp in _data.Connections select kvp.Key).ToList();
            var connectionId = connections[_rand.Next(connections.Count)];
            Clients.Client(connectionId).SendAsync("client", message);
        }

        public void SendToUserRandomly(string message)
        {
            var userId = GetUniqueName(_rand.Next(_data.Connections.Count));
            Clients.User(userId).SendAsync("user", message);
        }

        public void JoinGroup(string groupName)
        {
            Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public void LeaveGroup(string groupName)
        {
            Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public void SendToGroupRandomly(string message)
        {
            var groupName = GetUniqueName(_rand.Next(_data.Connections.Count));
            Clients.Group(groupName).SendAsync("group", message);
        }

        // todo: share with client and server
        public string GetUniqueName(int index)
        {
            return $"{_data.Prefix}.{index}";
        }
    }
}
