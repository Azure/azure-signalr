// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Tests.Common;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestHub : Hub
    {
        private TestHubConnectionManager _testHubConnectionManager;

        public TestHub(TestHubConnectionManager testHubConnectionManager)
        {
            _testHubConnectionManager = testHubConnectionManager;
        }

        public override Task OnConnectedAsync()
        {
            _testHubConnectionManager.AddClient(Context.ConnectionId);
            _testHubConnectionManager.AddUser(Context.UserIdentifier);
            return Task.CompletedTask;
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            _testHubConnectionManager.RemoveClient(Context.ConnectionId);
            _testHubConnectionManager.RemoveUser(Context.UserIdentifier);
            return Task.CompletedTask;
        }

        public void Echo(string message)
        {
            Clients.Caller.SendAsync(nameof(Echo), message);
        }

        public void SendToClient(string message)
        {
            var ind = StaticRandom.Next(0, _testHubConnectionManager.ClientCount);
            Clients.Client(_testHubConnectionManager.Clients[ind]).SendAsync(nameof(SendToClient), message);
        }

        public void SendToUser(string message)
        {
            var ind = StaticRandom.Next(0, _testHubConnectionManager.UserCount);
            Clients.User(_testHubConnectionManager.Users[ind]).SendAsync(nameof(SendToUser), message);
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