// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Threading.Tasks;

using ModelContextProtocol.Server;

namespace ChatMcp;

[McpServerToolType]
public class ChatTool(ChatProxy proxy)
{
    [McpServerTool]
    [Description("Broadcast a message to all clients.")]
    public async Task BroadcastMessage(
        [Description("The text message.")]
        string message)
    {
        await proxy.BroadcastMessage("MCP", message);
    }
}
