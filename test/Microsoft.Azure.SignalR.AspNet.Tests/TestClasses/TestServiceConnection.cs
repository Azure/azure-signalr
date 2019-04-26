// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal sealed class TestServiceConnection : IServiceConnection
    {
        public ServiceConnectionStatus Status { get; }

        public Task ConnectionInitializedTask => Task.CompletedTask;

        private readonly bool _throws;
        public TestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected, bool throws = false)
        {
            Status = status;
            _throws = throws;
        }

        public Task StartAsync(string target = null, string productInfo = null)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_throws)
            {
                throw new ServiceConnectionNotActiveException();
            }

            return Task.CompletedTask;
        }
    }
}
