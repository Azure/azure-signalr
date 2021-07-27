// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ConnectionService : IHostedService
    {
        private readonly IServiceConnectionContainer _connectionContainer;

        public ConnectionService(IServiceConnectionContainer connectionContainer)
        {
            _connectionContainer = connectionContainer;
        }

        public Task StartAsync(CancellationToken token)
        {
            _ = _connectionContainer.StartAsync();
            return _connectionContainer.ConnectionInitializedTask.OrTimeout(token, TimeSpan.FromMinutes(1), "establishing service connections");
        }

        public Task StopAsync(CancellationToken _)
        {
            return _connectionContainer.StopAsync();
        }
    }
}