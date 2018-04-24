// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Internal;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR
{
    internal class HeartBeat : BackgroundService
    {
        private static readonly TimeSpan _heartbeatTickRate = TimeSpan.FromSeconds(1);
        private readonly IClientConnectionManager _connectionManager;
        private readonly TimerAwaitable _nextHeartbeat;

        public HeartBeat(IClientConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
            _nextHeartbeat = new TimerAwaitable(_heartbeatTickRate, _heartbeatTickRate);
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
                        connection.Value.TickHeartbeat();
                    }
                }
            }
        }
    }
}
