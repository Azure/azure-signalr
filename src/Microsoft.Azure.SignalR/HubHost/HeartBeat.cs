// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR;

internal class HeartBeat : BackgroundService
{
    private static readonly TimeSpan HeartbeatTickRate = TimeSpan.FromSeconds(1);

    private readonly IClientConnectionManager _connectionManager;
    private readonly ICultureFeatureManager _cultureFeatureManager;

    private readonly TimerAwaitable _nextHeartbeat;

    public HeartBeat(IClientConnectionManager connectionManager, ICultureFeatureManager cultureFeatureManager)
    {
        _connectionManager = connectionManager;
        _cultureFeatureManager = cultureFeatureManager;
        _nextHeartbeat = new TimerAwaitable(HeartbeatTickRate, HeartbeatTickRate);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // This will get called when
        _nextHeartbeat.Start();

        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _nextHeartbeat.Stop();

        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Dispose the timer when all the code consuming callbacks has completed
        using (_nextHeartbeat)
        {
            // The TimerAwaitable will return true until Stop is called
            while (await _nextHeartbeat)
            {
                // Trigger each connection heartbeat
                foreach (var connection in _connectionManager.ClientConnections)
                {
                    (connection as ClientConnectionContext).TickHeartbeat();
                }
                _cultureFeatureManager.Cleanup();
            }
        }
    }
}
