// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

internal sealed class TestServiceConnectionHandler : ServiceConnectionManager
{
    private readonly ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>> _waitForTransportOutputMessage = new ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>>();

    public TestServiceConnectionHandler() : this(null, null)
    {
    }

    public TestServiceConnectionHandler(string appName, IReadOnlyList<string> hubs) : base(appName, hubs)
    {
    }

    public override Task WriteAsync(ServiceMessage serviceMessage)
    {
        if (_waitForTransportOutputMessage.TryGetValue(serviceMessage.GetType(), out var tcs))
        {
            tcs.SetResult(serviceMessage);
        }

        return Task.CompletedTask;
    }

    public override Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
    {
        if (_waitForTransportOutputMessage.TryGetValue(serviceMessage.GetType(), out var tcs))
        {
            tcs.SetResult(serviceMessage);
        }
        else
        {
            throw new InvalidOperationException("Not expected to write before tcs is inited");
        }

        return Task.FromResult(true);
    }

    public Task<ServiceMessage> WaitForTransportOutputMessageAsync(Type messageType)
    {
        if (_waitForTransportOutputMessage.TryGetValue(messageType, out var tcs))
        {
            tcs.TrySetCanceled();
        }

        // re-init the tcs
        tcs = _waitForTransportOutputMessage[messageType] = new TaskCompletionSource<ServiceMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        return tcs.Task;
    }

    public void DisposeServiceConnection(IServiceConnection connection)
    {
    }
}
