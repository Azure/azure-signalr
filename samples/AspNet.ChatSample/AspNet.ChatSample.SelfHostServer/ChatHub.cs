// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.AspNet.SignalR;

namespace AspNet.ChatSample.SelfHostServer
{
    public class ChatHub : Hub
    {
        public void BroadcastMessage(string name, string message)
        {
            Clients.All.roadcastMessage("broadcastMessage", name, message);
            Trace.TraceInformation("Broadcasting...");
        }

        public void Echo(string message)
        {
            Clients.Client(Context.ConnectionId).Echo($"{message} (echo from server, Client IP: {GetIpAddress()})");
            Trace.TraceInformation("Echo...");
        }

        private string GetIpAddress()
        {
            object address = null;
            Context.Request.Environment?.TryGetValue("server.RemoteIpAddress", out address);
            return address as string;
        }
    }
}
