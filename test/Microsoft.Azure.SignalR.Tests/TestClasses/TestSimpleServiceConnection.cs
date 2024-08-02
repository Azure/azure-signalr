// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests;

internal sealed class TestSimpleServiceConnection : IServiceConnection
{
    private readonly bool _throws;

    private readonly TaskCompletionSource<object> _writeAsyncTcs = null;

    public ServiceConnectionStatus Status { get; }

    public Task ConnectionInitializedTask => Task.CompletedTask;

    public Task ConnectionOfflineTask => Task.CompletedTask;

    public string ConnectionId => throw new NotImplementedException();

    public string ServerId => throw new NotImplementedException();

    public TestSimpleServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected,
                                       bool throws = false,
                                       TaskCompletionSource<object> writeAsyncTcs = null)
    {
        Status = status;
        _throws = throws;
        _writeAsyncTcs = writeAsyncTcs;
    }

    public event Action<StatusChange> ConnectionStatusChanged;

    public Task StartAsync(string target = null)
    {
        ConnectionStatusChanged?.Invoke(new StatusChange(ServiceConnectionStatus.Connecting, Status));

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        ConnectionStatusChanged?.Invoke(new StatusChange(Status, ServiceConnectionStatus.Disconnected));
        return Task.CompletedTask;
    }

    public Task WriteAsync(ServiceMessage serviceMessage)
    {
        if (_throws)
        {
            throw new ServiceConnectionNotActiveException();
        }

        _writeAsyncTcs?.TrySetResult(null);
        return Task.CompletedTask;
    }

    public Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
    {
        if (_throws)
        {
            return Task.FromResult(false);
        }

        _writeAsyncTcs?.TrySetResult(null);
        return Task.FromResult(true);
    }
}
