// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;

namespace AspNet.ChatSample.SelfHostServer
{
    public class ChatHub : Hub
    {
        public override Task OnConnected()
        {
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected()
        {
            return base.OnReconnected();
        }

        public void BroadcastMessage(string name, string message)
        {
            Clients.All.BroadcastMessage(name, message);
            Trace.TraceInformation("Broadcasting...");
        }

        public void Echo(string message)
        {
            Clients.Caller.Echo($"{message} (echo from server, Client IP: {GetIpAddress()})");
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
