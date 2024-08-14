// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal class DynamicConnectionHandler : ConnectionHandler
    {
        private readonly IDynamicHubContextStore _store;

        public DynamicConnectionHandler(IDynamicHubContextStore store)
        {
            _store = store;
        }

        public override Task OnConnectedAsync(ConnectionContext connection)
        {
            var httpContext = connection.GetHttpContext();
            var hub = httpContext.Request.Query["hub"];
            if (string.IsNullOrEmpty(hub))
            {
                throw new ArgumentException(hub);
            }

            var lifetime = _store.GetOrAdd(hub);
            var connectionHandler = lifetime.ConnectionHandler;
            return connectionHandler.OnConnectedAsync(connection);
        }
    }
}
