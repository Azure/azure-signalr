// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests;

internal class TestServiceConnectionManager<THub> : IServiceConnectionManager<THub> where THub : Hub
{
    private readonly ConcurrentDictionary<Type, int> _writeAsyncCallCount = new ConcurrentDictionary<Type, int>();

    private readonly ConcurrentDictionary<Type, int> _partitionedWriteAsyncCallCount = new ConcurrentDictionary<Type, int>();

    public ServiceMessage ServiceMessage { get; private set; }

    public void SetServiceConnection(IServiceConnectionContainer serviceConnection)
    {
    }

    public async Task StartAsync()
    {
        await Task.CompletedTask;
    }

    public Task WriteAsync(ServiceMessage serviceMessage)
    {
        _writeAsyncCallCount.AddOrUpdate(serviceMessage.GetType(), 1, (_, value) => value + 1);
        ServiceMessage = serviceMessage;
        return Task.CompletedTask;
    }

    public async Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
    {
        if (serviceMessage is IAckableMessage)
        {
            await WriteAsync(serviceMessage);
            return true;
        }
        return true;
    }

    public int GetCallCount(Type type)
    {
        return _writeAsyncCallCount.TryGetValue(type, out var count) ? count : 0;
    }

    public int GetPartitionedCallCount(Type type)
    {
        return _partitionedWriteAsyncCallCount.TryGetValue(type, out var count) ? count : 0;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token) => Task.CompletedTask;

    public Task CloseClientConnections(CancellationToken token) => Task.CompletedTask;
}
