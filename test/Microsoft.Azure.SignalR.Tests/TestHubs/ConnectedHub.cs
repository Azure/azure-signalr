// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Tests.TestHubs;

internal class ConnectedHub : Hub
{
    private readonly ILogger<ConnectedHub> _logger;

    public ConnectedHub(ILogger<ConnectedHub> logger)
    {
        _logger = logger;
    }
    public override async Task OnConnectedAsync()
    {
        while (!Context.ConnectionAborted.IsCancellationRequested)
        {
            await Clients.Clients(Context.ConnectionId).SendAsync("hello");
            await Task.Delay(100);
        }
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        _logger.LogInformation($"{Context.ConnectionId} disconnected: {exception}.");
        return Task.CompletedTask;
    }
}
