// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR;

namespace ChatSample
{
    public class TimeService
    {
        private readonly HubProxy _hubProxy;
        private readonly Timer _timer;

        public TimeService(HubProxy hubProxy)
        {
            _hubProxy = hubProxy ?? throw new ArgumentNullException(nameof(hubProxy));
            _timer = new Timer(Run, this, 100, 60 * 1000);
        }

        private static void Run(object state)
        {
            _ = ((TimeService)state).Broadcast();
        }

        private async Task Broadcast()
        {
            await _hubProxy.Clients.All.SendAsync("broadcastMessage",
                new object[]
                {
                    "_BROADCAST_",
                    DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)
                });
        }
    }
}
