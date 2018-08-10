// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionManager : IServiceConnectionManager
    {
        public void AddConnection(string hubName, IServiceConnection connection)
        {
            throw new System.NotImplementedException();
        }

        public Task StartAsync()
        {
            throw new System.NotImplementedException();
        }

        public Task StopAsync()
        {
            throw new System.NotImplementedException();
        }

        public IServiceConnection WithHub(string hubName)
        {
            throw new System.NotImplementedException();
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            throw new System.NotImplementedException();
        }

        public Task WriteAsync(string connectionId, object message)
        {
            throw new System.NotImplementedException();
        }
    }
}
