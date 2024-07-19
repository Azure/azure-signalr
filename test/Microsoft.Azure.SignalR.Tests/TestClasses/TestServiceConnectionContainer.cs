// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests;

internal sealed class TestServiceConnectionContainer : ServiceConnectionContainerBase
{
    public bool IsOffline { get; set; } = false;

    public bool MockOffline { get; set; } = false;

    public TestServiceConnectionContainer(List<IServiceConnection> serviceConnections, HubServiceEndpoint endpoint = null, AckHandler ackHandler = null, IServiceConnectionFactory factory = null, ILogger logger = null)
        : base(factory, 0, endpoint, serviceConnections, ackHandler: ackHandler, logger: logger ?? NullLogger.Instance)
    {
    }

    public List<IServiceConnection> Connections { get => ServiceConnections; }

    public void ShutdownForTest()
    {
        var prop = typeof(ServiceConnectionContainerBase).GetField("_terminated", BindingFlags.NonPublic | BindingFlags.Instance);
        prop.SetValue(this, true);
    }

    public override async Task OfflineAsync(GracefulShutdownMode mode)
    {
        if (MockOffline)
        {
            await Task.Delay(100);
            IsOffline = true;
        }
        else
        {
            await base.OfflineAsync(mode);
        }
    }

    public override Task HandlePingAsync(PingMessage pingMessage)
    {
        return Task.CompletedTask;
    }

    public Task BaseHandlePingAsync(PingMessage pingMessage)
    {
        return base.HandlePingAsync(pingMessage);
    }

    protected override Task OnConnectionComplete(IServiceConnection connection)
    {
        return Task.CompletedTask;
    }

    public Task OnConnectionCompleteForTestShutdown(IServiceConnection connection)
    {
        return base.OnConnectionComplete(connection);
    }

    public Task MockReceivedServersPing(string serversTag)
    {
        var ping = new PingMessage { Messages = new[] { "servers", $"{DateTime.UtcNow.Ticks}:{serversTag}" } };
        return base.HandlePingAsync(ping);
    }

    public Task MockReceivedStatusPing(bool isActive)
    {
        var ping = new PingMessage { Messages = new[] { "status", isActive ? "1" : "0" } };
        return base.HandlePingAsync(ping);
    }

    public Task MockReceivedStatusPing(bool isActive, int clientCount)
    {
        var ping = new PingMessage { Messages = new[] { "status", isActive ? "1" : "0", "clientcount", clientCount.ToString() } };
        return base.HandlePingAsync(ping);
    }
}
