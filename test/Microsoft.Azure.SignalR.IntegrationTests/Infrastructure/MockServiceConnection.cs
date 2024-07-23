// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Encapsulates the actual ServiceConnection to facilitate sync up of MockService and SDK connections
    /// </summary>
    internal class MockServiceConnection : IServiceConnection
    {
        private static int s_num = 0;

        private readonly IServiceConnection _serviceConnection;
        private IMockService _mockService;
        
        internal MockServiceConnection(IMockService mockService, IServiceConnection serviceConnection)
        {
            _mockService = mockService;
            _serviceConnection = serviceConnection;
            ConnectionNumber = Interlocked.Increment(ref s_num);
            _mockService.RegisterSDKConnection(this);
        }

        public int ConnectionNumber { get; private set; }

        public IServiceConnection InnerServiceConnection => _serviceConnection;

        public MockServiceConnectionContext MyConnectionContext { get; set; }

        public ServiceConnectionStatus Status => _serviceConnection.Status;

        public Task ConnectionInitializedTask => _serviceConnection.ConnectionInitializedTask;

        public Task ConnectionOfflineTask => _serviceConnection.ConnectionOfflineTask;

        public Task StartAsync(string target = null)
        {
            var tag = $"svc_{ConnectionNumber}_";
            target = tag + target;
            return _serviceConnection.StartAsync(target);
        }

        public Task StopAsync() => _serviceConnection.StopAsync();

        public Task WriteAsync(ServiceMessage serviceMessage) => _serviceConnection.WriteAsync(serviceMessage);

        public async Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
        {
            await WriteAsync(serviceMessage);
            return true;
        }

        public event Action<StatusChange> ConnectionStatusChanged
        {
            add => _serviceConnection.ConnectionStatusChanged += value;
            remove => _serviceConnection.ConnectionStatusChanged -= value;
        }
    }
}
