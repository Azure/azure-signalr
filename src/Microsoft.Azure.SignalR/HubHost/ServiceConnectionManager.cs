﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR
{
    public class ServiceConnectionManager : IServiceConnectionManager
    {
        private readonly List<ServiceConnection> _serviceConnections = new List<ServiceConnection>();

        public void AddServiceConnection(ServiceConnection serviceConnection)
        {
            _serviceConnections.Add(serviceConnection);
        }

        public async Task StartAllServiceConnection(ConnectionDelegate connectionDelegate)
        {
            var tasks = _serviceConnections.Select(c => c.StartAsync(connectionDelegate));
            await Task.WhenAll(tasks);
        }

        public async Task SendServiceMessage(ServiceMessage serviceMessage)
        {
            var index = StaticRandom.Next(_serviceConnections.Count);
            await _serviceConnections[index].SendServiceMessage(serviceMessage);
        }
    }
}
