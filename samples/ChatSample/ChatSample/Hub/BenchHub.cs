// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace ChatSample.CoreApp3
{
    public class BenchHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Client(Context.ConnectionId).SendAsync("OnConnected", Context.ConnectionId);
        }

        public void Echo(IDictionary<string, object> data)
        {
            Clients.Caller.SendAsync("RecordLatency", data);
        }

        public ChannelReader<IDictionary<string, object>> StreamingEcho(ChannelReader<IDictionary<string, object>> stream, int delay)
        {
            var channel = Channel.CreateUnbounded<IDictionary<string, object>>();
            async Task WriteChannelStream()
            {
                Exception localException = null;
                try
                {
                    while (await stream.WaitToReadAsync())
                    {
                        while (stream.TryRead(out var item))
                        {
                            await channel.Writer.WriteAsync(item);
                            if (delay > 0)
                            {
                                await Task.Delay(delay);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    localException = ex;
                }
                channel.Writer.Complete(localException);
            }
            _ = WriteChannelStream();
            return channel.Reader;
        }

        public void Broadcast(IDictionary<string, object> data)
        {
            Clients.All.SendAsync("RecordLatency", data);
        }

        public void SendToClient(IDictionary<string, object> data)
        {
            var targetId = data["information.ConnectionId"].ToString();
            Clients.Client(targetId).SendAsync("RecordLatency", data);
        }

        public void ConnectionId()
        {
            Clients.Client(Context.ConnectionId).SendAsync("ConnectionId", Context.ConnectionId);
        }

        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Client(Context.ConnectionId).SendAsync("JoinGroup");
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Client(Context.ConnectionId).SendAsync("LeaveGroup");
        }

        public void SendToGroup(IDictionary<string, object> data)
        {
            var groupName = data["information.GroupName"].ToString();
            Clients.Group(groupName).SendAsync("RecordLatency", data);
        }
    }

}
