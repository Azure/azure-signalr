// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Transports;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

public class TestTransport : IServiceTransport
{
    private readonly TaskCompletionSource<object> _lifetimeTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    public long MessageCount = 0;
    public Func<string, Task> Received { get; set; }
    public Func<Task> Connected { get; set; }
    public Func<Task> Reconnected { get; set; }
    public Func<bool, Task> Disconnected { get; set; }
    public string ConnectionId { get; set; }

    public Task<string> GetGroupsToken()
    {
        return Task.FromResult<string>(null);
    }

    public void OnDisconnected() => _lifetimeTcs.TrySetResult(null);

    public void OnReceived(string value)
    {
        // Only use to validate message count
        MessageCount++;
    }

    public Task ProcessRequest(ITransportConnection connection)
    {
        return Task.CompletedTask;
    }

    public Task Send(object value)
    {
        return Task.CompletedTask;
    }

    public Task WaitOnDisconnected()
    {
        return _lifetimeTcs.Task;
    }
}
