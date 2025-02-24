// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR.E2ETests.Common;

public class WebSocketProxy(string targetUri)
{
    public async Task HandleWebSocket(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();
        using var serverSocket = new ClientWebSocket();

        await serverSocket.ConnectAsync(new Uri(targetUri), CancellationToken.None);

        var clientToServer = RelayMessages(clientSocket, serverSocket);
        var serverToClient = RelayMessages(serverSocket, clientSocket);

        await Task.WhenAny(clientToServer, serverToClient);
    }

    public void Start(int listenPort)
    {
        Host.CreateDefaultBuilder([])
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseKestrel(options => options.ListenAnyIP(listenPort));
                webBuilder.Configure(app =>
                {
                    app.UseWebSockets();
                    app.Run(HandleWebSocket);
                });
            })
            .Build()
            .Run();
    }

    private static async Task RelayMessages(WebSocket source, WebSocket destination)
    {
        var buffer = new byte[4096];

        try
        {
            while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open)
            {
                var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await destination.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                await destination.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket Error: {ex.Message}");
        }
    }
}
