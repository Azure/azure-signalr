// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Microsoft.Azure.SignalR.AspNet.Tests.TestHubs
{
    [HubName("EndlessInvoke")]
    public class EndlessInvokeHub : Hub
    {
        public override Task OnConnected()
        {
            Clients.Group("note").echo("Connected");
            return Task.CompletedTask;
        }

        public override Task OnReconnected()
        {
            Clients.Group("note").echo("Reconnected");
            return Task.CompletedTask;
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            Clients.Group("note").echo("Disconnected");
            return Task.CompletedTask;
        }

        public void BroadcastMessage(string name, string message)
        {
            Clients.All.broadcastMessage(name, message);
        }

        public void Echo(string name, string message)
        {
            Clients.Caller.echo(name, message + " (echo from server)");
        }

        public async Task JoinGroup(string name, string groupName)
        {
            Clients.Group(groupName).echo("_SYSTEM_", $"{name} joined {groupName} with connectionId {Context.ConnectionId}");
            while (true)
            {
                await Task.Delay(1000);
            }
        }

        public async Task LeaveGroup(string name, string groupName)
        {
            await Groups.Remove(Context.ConnectionId, groupName);
            Clients.Group(groupName).echo("_SYSTEM_", $"{name} leaved {groupName}");
        }

        public Task SendGroup(string name, string groupName, string message)
        {
            Clients.Group(groupName).echo(name, message);
            return Task.CompletedTask;
        }

        public void SendGroups(string name, IList<string> groups, string message)
        {
            Clients.Groups(groups).echo(name, message);
        }

        public void SendGroupExcept(string name, string groupName, string[] connectionIdExcept, string message)
        {
            Clients.Groups(new List<string> { groupName }, connectionIdExcept).echo(name, message);
        }

        public void SendUser(string name, string userId, string message)
        {
            Clients.User(userId).echo(name, message);
        }

        public void SendUsers(string name, IList<string> userIds, string message)
        {
            Clients.Users(userIds).echo(name, message);
        }
    }
}
