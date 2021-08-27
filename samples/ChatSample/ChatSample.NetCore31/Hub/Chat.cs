// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;

namespace ChatSample.CoreApp3
{
    public class Chat : Hub
    {
        private readonly IHubContext<Chat> _context;

        public Chat(IHubContext<Chat> context)
        {
            _context = context;
        }

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
                new HeartBeatContext(_context, Context.Features.Get<IConnectionStatFeature>(), Context.ConnectionId));

            var feature = Context.Features.Get<IConnectionMigrationFeature>();
            if (feature != null)
            {
                Console.WriteLine($"[{feature.MigrateTo}] {Context.ConnectionId} is migrated from {feature.MigrateFrom}.");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception e)
        {
            Console.WriteLine($"{Context.ConnectionId} disconnected.");

            var feature = Context.Features.Get<IConnectionMigrationFeature>();
            if (feature != null)
            {
                Console.WriteLine($"[{feature.MigrateFrom}] {Context.ConnectionId} will be migrated to {feature.MigrateTo}.");
            }

            await base.OnDisconnectedAsync(e);
        }

        private sealed class HeartBeatContext
        {
            private readonly IHubContext<Chat> _context;
            private readonly IConnectionStatFeature _stat;
            private DateTime _lastMessageReceivedAt;
            private readonly string _connectionId;

            public HeartBeatContext(IHubContext<Chat> context, IConnectionStatFeature stat, string connectionId)
            {
                _context = context;
                _stat = stat;
                _connectionId = connectionId;
            }

            public void HeartBeat()
            {
                if (_stat.LastMessageReceivedAtUtc != _lastMessageReceivedAt)
                {
                    _ = _context.Clients.Client(_connectionId).SendAsync(
                        "echo",
                        "sys",
                        $"last recieve message at: {_stat.LastMessageReceivedAtUtc}, total size: {_stat.ReceivedBytes}.");
                    _lastMessageReceivedAt = _stat.LastMessageReceivedAtUtc;
                }
            }
        }
    }
}