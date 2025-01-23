// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Tests.TestHubs;

internal sealed class SimpleHub : Hub
{
    private readonly ILogger<SimpleHub> _logger;

    public SimpleHub(ILogger<SimpleHub> logger)
    {
        _logger = logger;
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        _logger.LogInformation($"{Context.ConnectionId} disconnected: {exception}.");
        return Task.CompletedTask;
    }
}
