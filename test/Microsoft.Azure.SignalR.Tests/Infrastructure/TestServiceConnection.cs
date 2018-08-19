// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests
{
    public class TestServiceConnection : IServiceConnection
    {
        public IList<ServiceMessage> Messages { get; } = new List<ServiceMessage>();

        public Task StartAsync() => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            Messages.Add(serviceMessage);
            return Task.CompletedTask;
        }
    }
}
