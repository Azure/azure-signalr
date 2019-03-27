using Microsoft.AspNetCore.SignalR;
using System;

namespace ChatSample.Core3
{
    public class Chat : Hub
    {
        public void BroadcastMessage(string name, string message)
        {
            Clients.All.SendAsync("broadcastMessage", name, message);
            Console.WriteLine("Broadcasting...");
        }

        public void Echo(string name, string message)
        {
            Clients.Client(Context.ConnectionId).SendAsync("echo", name, $"{message} (echo from server, Client IP: {Context.GetHttpContext().Connection.RemoteIpAddress})");
            Console.WriteLine("Echo...");
        }
    }

    public class NotificationHub : Hub
    {

    }
}