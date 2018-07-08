// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests.Infrastructure
{
    class TestServiceConnectionManager<THub> : IServiceConnectionManager<THub> where THub : Hub
    {
        private readonly ConcurrentDictionary<Type, int> _writeAsyncCallCount = new ConcurrentDictionary<Type, int>();

        public ServiceMessage ServiceMessage { get; private set; }

        public void AddServiceConnection(ServiceConnection serviceConnection)
        {
        }

        public async Task StartAsync()
        {
            await Task.CompletedTask;
        }

        public async Task WriteAsync(ServiceMessage serviceMessage)
        {
            _writeAsyncCallCount.AddOrUpdate(serviceMessage.GetType(), 1, (_, value) => value + 1);
            ServiceMessage = serviceMessage;
            await Task.CompletedTask;
        }

        public int GetCallCount(Type type)
        {
            if (_writeAsyncCallCount.TryGetValue(type, out var count))
            {
                return count;
            }

            return 0;
        }
    }
}
