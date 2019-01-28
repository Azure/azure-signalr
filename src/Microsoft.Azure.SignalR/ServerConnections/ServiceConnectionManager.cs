// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnectionManager<THub> : IServiceConnectionManager<THub> where THub : Hub
    {
        private IServiceConnectionContainer _serviceConnection = null;

        public void SetServiceConnection(IServiceConnectionContainer serviceConnection)
        {
            _serviceConnection = serviceConnection;
        }

        public Task StartAsync()
        {
            return _serviceConnection.StartAsync();
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_serviceConnection == null)
            {
                throw new AzureSignalRNotConnectedException();
            }

            return _serviceConnection.WriteAsync(serviceMessage);
        }

        public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            // If we hit this check, it is a code bug.
            if (string.IsNullOrEmpty(partitionKey))
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (_serviceConnection == null)
            {
                throw new AzureSignalRNotConnectedException();
            }

            return _serviceConnection.WriteAsync(partitionKey, serviceMessage);
        }

        public Task WriteAndWaitForAckAsync(ServiceMessage serviceMessage)
        {
            if (_serviceConnection == null)
            {
                throw new AzureSignalRNotConnectedException();
            }

            return _serviceConnection.WriteAndWaitForAckAsync(serviceMessage);
        }
    }
}
