// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;

namespace Microsoft.Azure.SignalR.E2ETest
{
    public class E2ETestHub : Hub
    {
        public Data _data;
        private Random _rand = new Random();

        public E2ETestHub(Data data)
        {
            _data = data;
        }

        public override Task OnConnected()
        {
            _data.Connections.AddOrUpdate(Context.ConnectionId, 0, (k, v) => 0);
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            _data.Connections.TryRemove(Context.ConnectionId, out _);
            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected()
        {
            return base.OnReconnected();
        }

        public void Broadcast(string message)
        {
            Clients.All.broadcast(message);
        }

        public void Echo(string message)
        {
            Clients.Client(Context.ConnectionId).echo(message);
        }

        public void SendToClientRandomly(string message)
        {
            var connections = (from kvp in _data.Connections select kvp.Key).ToList();
            var connectionId = connections[_rand.Next(connections.Count)];
            Clients.Client(connectionId).client(message);
        }

        public void SendToUserRandomly(string message)
        {
            var userId = Utils.GetUniqueName(_data.Prefix, _rand.Next(_data.Connections.Count));
            Clients.User(userId).user(message);
        }

        public void JoinGroup(string groupName)
        {
            Groups.Add(Context.ConnectionId, groupName);
        }

        public void LeaveGroup(string groupName)
        {
            Groups.Remove(Context.ConnectionId, groupName);
        }

        public void SendToGroupRandomly(string message)
        {
            var groupName = Utils.GetUniqueName(_data.Prefix, _rand.Next(_data.Connections.Count));
            Clients.Group(groupName).group(message);
        }
    }
}
