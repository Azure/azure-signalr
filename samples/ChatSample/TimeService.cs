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
        private readonly SignalRServiceContext<Chat> _hubProxy;
        private Timer _timer;
        private bool _isDisposed;

        public TimeService(CloudSignalR cloudSignalR)
        {
            _hubProxy = cloudSignalR.CreateServiceContext<Chat>();
        }

        public void Start()
        {
            _timer = new Timer(Run, this, 100, 60 * 1000);
            _isDisposed = false;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _timer.Dispose();
            }
            _isDisposed = true;
        }

        private static void Run(object state)
        {
            _ = ((TimeService)state).Broadcast();
        }

        private async Task Broadcast()
        {
            await _hubProxy.HubContext.Clients.All.SendCoreAsync("broadcastMessage",
                new object[]
                {
                    "_BROADCAST_",
                    DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)
                });
        }
    }
}
