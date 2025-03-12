// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;

namespace ChatSample;

public class ChatHub(IHubContext<ChatHub> context) : Hub
{
    public void BroadcastMessage(string name, string message)
    {
        Clients.All.SendAsync("broadcastMessage", name, message);
        Console.WriteLine("Broadcasting...");
    }

    public void Echo(string name, string message)
    {
        Clients.Caller.SendAsync("echo", name,
            $"{message} (echo from server, Client IP: {Context.GetHttpContext().Connection.RemoteIpAddress}");
        Console.WriteLine("Echo...");
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"{Context.ConnectionId} connected.");
        Context.Features.Get<IConnectionHeartbeatFeature>().OnHeartbeat(
            c => ((HeartBeatContext)c).HeartBeat(),
            new HeartBeatContext(context, Context.Features.Get<IConnectionStatFeature>(), Context.ConnectionId));

        var feature = Context.Features.Get<IConnectionMigrationFeature>();
        if (feature != null)
        {
            Console.WriteLine($"[{feature.MigrateTo}] {Context.ConnectionId} is migrated from {feature.MigrateFrom}.");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        Console.WriteLine($"{Context.ConnectionId} disconnected.");

        var feature = Context.Features.Get<IConnectionMigrationFeature>();
        if (feature != null)
        {
            Console.WriteLine($"[{feature.MigrateFrom}] {Context.ConnectionId} will be migrated to {feature.MigrateTo}.");
        }

        await base.OnDisconnectedAsync(exception);
    }

    private sealed class HeartBeatContext(IHubContext<ChatHub> context, IConnectionStatFeature stat, string connectionId)
    {
        private readonly IHubContext<ChatHub> _context = context;

        private DateTime _lastMessageReceivedAt;

        public void HeartBeat()
        {
            if (stat.LastMessageReceivedAtUtc != _lastMessageReceivedAt)
            {
                _ = _context.Clients.Client(connectionId).SendAsync(
                    "echo",
                    "sys",
                    $"last recieve message at: {stat.LastMessageReceivedAtUtc}, total size: {stat.ReceivedBytes}.");
                _lastMessageReceivedAt = stat.LastMessageReceivedAtUtc;
            }
        }
    }
}
