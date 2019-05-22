// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Tests
{
    public class TestHub : Hub
    {
        private ConcurrentDictionary<string, bool> _connectedConnections = new ConcurrentDictionary<string, bool>();

        //public override Task OnConnectedAsync()
        //{
        //    if (!_connectedConnections.TryAdd(Context.ConnectionId, false))
        //    {
        //        throw new InvalidOperationException($"Failed to add a client connection.");
        //    }
        //    return Task.CompletedTask;
        //}

        //public override Task OnDisconnectedAsync(Exception exception)
        //{
        //    if (!_connectedConnections.TryRemove(Context.ConnectionId, out _))
        //    {
        //        throw new InvalidOperationException($"Failed to remove a client connection.");
        //    }
        //    return Task.CompletedTask;
        //}

        public void Echo(string message)
        {
            Clients.Client(Context.ConnectionId).SendAsync(nameof(Echo), message);
        }

        public void SendToClient(string message)
        {
            var ind = StaticRandom.Next(0, _connectedConnections.Count);
            Clients.Client(_connectedConnections.Keys.ToList()[ind]).SendAsync(nameof(SendToClient), message);
        }
    }
}