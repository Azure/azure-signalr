// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal sealed class TestServiceConnectionContainer : IServiceConnectionContainer, IServiceConnection
{
    private readonly Action<(ServiceMessage, IServiceConnectionContainer)> _validator;

    public string HubName { get; }

    public ServiceConnectionStatus Status { get; }

    public Task ConnectionInitializedTask => Task.CompletedTask;

    public Task ConnectionOfflineTask => Task.CompletedTask;

    public IReadOnlyDictionary<ServiceEndpoint, IServiceConnectionContainer> ConnectionContainers { get; }

    public string ConnectionId => throw new NotImplementedException();

    public string ServersTag => throw new NotSupportedException();

    public string ServerId => throw new NotImplementedException();

    public bool HasClients => throw new NotSupportedException();

    public TestServiceConnectionContainer(ServiceConnectionStatus status)
    {
        Status = status;
    }

    public TestServiceConnectionContainer(string name, Action<(ServiceMessage, IServiceConnectionContainer)> validator)
    {
        _validator = validator;
        HubName = name;
    }

    public event Action<StatusChange> ConnectionStatusChanged;

    public Task StartAsync()
    {
        ConnectionStatusChanged?.Invoke(new StatusChange(ServiceConnectionStatus.Connecting, Status));
        return Task.CompletedTask;
    }

    public Task StartAsync(string target)
    {
        return Task.CompletedTask;
    }

    public Task WriteAsync(ServiceMessage serviceMessage)
    {
        _validator?.Invoke((serviceMessage, this));
        return Task.CompletedTask;
    }

    public async Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
    {
        try
        {
            await WriteAsync(serviceMessage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage,
        CancellationToken cancellationToken = default)
    {
        _validator?.Invoke((serviceMessage, this));
        return Task.FromResult(true);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public Task StartGetServersPing()
    {
        return Task.CompletedTask;
    }

    public Task StopGetServersPing()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}