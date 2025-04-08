// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using ChatSample;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChatMcp;

public sealed class ChatProxy(HubConnection connection) : IChatHub
{
    public async Task BroadcastMessage(string name, string message)
    {
        await connection.InvokeAsync(nameof(IChatHub.BroadcastMessage), name, message);
    }

    Task IChatHub.Echo(string name, string message)
    {
        // we dont need this ability in the MCP tool.
        throw new NotSupportedException();
    }
}
