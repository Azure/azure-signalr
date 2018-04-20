// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace ChatSample
{
    public class TimeService : BackgroundService
    {
        private readonly ServiceContext _hubProxy;
        private Timer _timer;
        private bool _isDisposed;

        public TimeService(IOptions<ServiceOptions> options)
        {
            string connectionString = null;
            // Look for connection string from:
            // 1. options
            // 2. environment variable
            // Throw exception if both 1 and 2 fail to find it.
            // So, please specify connection string through environment variable 
            // if Dependence Injection is not applied.
            if (String.IsNullOrEmpty(options.Value.ConnectionString))
            {
                connectionString = Environment.GetEnvironmentVariable(ServiceOptions.ConnectionStringDefaultKey);
            }
            else
            {
                connectionString = options.Value.ConnectionString;
            }
            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(ServiceOptions.ConnectionString));
            }
            // Please specify the hubname (case is insensitive) if Hub is running on remote.
            _hubProxy = CloudSignalR.CreateServiceContext(connectionString, typeof(Chat).Name);
        }

        private void Start()
        {
            _timer = new Timer(Run, this, 100, 60 * 1000);
            _isDisposed = false;
        }

        public override void Dispose()
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
            await _hubProxy.HubContext.Clients.All.SendAsync("broadcastMessage",
                new object[]
                {
                    "_BROADCAST_",
                    DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)
                });
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Start();
            return Task.CompletedTask;
        }
    }
}
