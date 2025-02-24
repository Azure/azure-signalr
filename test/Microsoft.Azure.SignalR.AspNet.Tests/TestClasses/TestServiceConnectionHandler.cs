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
    private readonly ConcurrentDictionary<Type, TaskCompletionSource<ServiceMessage>> _waitForTransportOutputMessage = new();

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
        else
        {
            throw new InvalidOperationException("Not expected to write before tcs is inited");
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
        // re-init the tcs
        var tcs = _waitForTransportOutputMessage.AddOrUpdate(messageType, t =>
        {
            return new TaskCompletionSource<ServiceMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        }, (t, old) =>
        {
            old.TrySetCanceled();
            return new TaskCompletionSource<ServiceMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        });
        return tcs.Task;
    }

    public void DisposeServiceConnection(IServiceConnection _)
    {
    }
}
