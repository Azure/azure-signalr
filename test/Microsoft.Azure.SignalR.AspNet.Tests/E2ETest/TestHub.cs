// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.Azure.SignalR.Tests.Common;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class TestHub : Hub
    {
        private TestHubConnectionManager _testHubConnectionManager;

        public TestHub(TestHubConnectionManager testHubConnectionManager)
        {
            _testHubConnectionManager = testHubConnectionManager;
        }

        public override Task OnConnected()
        {
            _testHubConnectionManager.AddClient(Context.ConnectionId);
            _testHubConnectionManager.AddUser(Context.Request.QueryString["user"]);
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            _testHubConnectionManager.RemoveClient(Context.ConnectionId);
            _testHubConnectionManager.RemoveUser(Context.Request.QueryString["user"]);
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
            var ind = StaticRandom.Next(0, _testHubConnectionManager.ClientCount);
            Clients.Client(_testHubConnectionManager.Clients[ind]).SendToClient(message);
        }

        public void SendToUser(string message)
        {
            var ind = StaticRandom.Next(0, _testHubConnectionManager.UserCount);
            Clients.User(_testHubConnectionManager.Users[ind]).SendToUser(message);
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