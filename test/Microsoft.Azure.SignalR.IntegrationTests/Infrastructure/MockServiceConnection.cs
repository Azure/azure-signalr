// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Encapsulates the actual ConnectionContext to facilitate sync up of MockService and SDK connections
    /// </summary>
    internal class MockServiceConnection : IServiceConnection
    {
        private readonly IServiceConnection _serviceConnection;
        IMockService _mockService;
        internal MockServiceConnection(IMockService mockService, IServiceConnection serviceConnection)
        {
            _mockService = mockService;
            _serviceConnection = serviceConnection;
        }

        public ServiceConnectionStatus Status => _serviceConnection.Status;

        public Task ConnectionInitializedTask => _serviceConnection.ConnectionInitializedTask;

        public Task ConnectionOfflineTask => _serviceConnection.ConnectionOfflineTask;

        public Task StartAsync(string target = null) => _serviceConnection.StartAsync(target);

        public Task StopAsync() => _serviceConnection.StopAsync();

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            var t = _serviceConnection.WriteAsync(serviceMessage);
            return t;
        }

        public event Action<StatusChange> ConnectionStatusChanged
        {
            add => _serviceConnection.ConnectionStatusChanged += value;
            remove => _serviceConnection.ConnectionStatusChanged -= value;
        }
    }
}
