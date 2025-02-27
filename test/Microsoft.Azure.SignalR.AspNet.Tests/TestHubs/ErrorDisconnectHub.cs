// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace Microsoft.Azure.SignalR.AspNet.Tests.TestHubs;

[HubName("ErrorDisconnect")]
public class ErrorDisconnectHub : Hub
{
    public override async Task OnConnected()
    {
        await Task.Yield();
        Trace.TraceInformation($"OnConnected {Context.ConnectionId}");
        Clients.Group("note").echo("Connected");
        Trace.TraceInformation($"Done Clients.Group(\"note\").echo(\"Connected\")");
    }

    public override Task OnReconnected()
    {
        Clients.Group("note").echo("Reconnected");
        return Task.CompletedTask;
    }

    public override Task OnDisconnected(bool stopCalled)
    {
        Clients.Group("note").echo("Disconnected");
        throw new InvalidOperationException("error disconnecting");
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
        await Groups.Add(Context.ConnectionId, groupName);
        Clients.Group(groupName).echo("_SYSTEM_", $"{name} joined {groupName} with connectionId {Context.ConnectionId}");
    }

    public async Task LeaveGroup(string name, string groupName)
    {
        await Groups.Remove(Context.ConnectionId, groupName);
        Clients.Group(groupName).echo("_SYSTEM_", $"{name} leaved {groupName}");
    }

    public void SendGroup(string name, string groupName, string message)
    {
        Clients.Group(groupName).echo(name, message);
    }

    public void SendGroups(string name, IList<string> groups, string message)
    {
        Clients.Groups(groups).echo(name, message);
    }

    public void SendGroupExcept(string name, string groupName, string[] connectionIdExcept, string message)
    {
        Clients.Groups([groupName], connectionIdExcept).echo(name, message);
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
