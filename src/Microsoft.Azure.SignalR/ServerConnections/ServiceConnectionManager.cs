// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

#nullable enable
namespace Microsoft.Azure.SignalR;

internal class ServiceConnectionManager<THub> : IDisposable, IServiceConnectionManager<THub> where THub : Hub
{
    private IServiceConnectionContainer? _serviceConnection;

    public void SetServiceConnection(IServiceConnectionContainer serviceConnection)
    {
        _serviceConnection = serviceConnection;
    }

    public Task StartAsync()
    {
        return _serviceConnection?.StartAsync() ?? throw new InvalidOperationException();
    }

    public Task StopAsync()
    {
        return _serviceConnection?.StopAsync() ?? Task.CompletedTask;
    }

    public Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token)
    {
        return _serviceConnection?.OfflineAsync(mode, token) ?? Task.CompletedTask;
    }

    public Task CloseClientConnections(CancellationToken cancellationToken)
    {
        return _serviceConnection?.CloseClientConnections(cancellationToken) ?? Task.CompletedTask;
    }

    public Task WriteAsync(ServiceMessage serviceMessage)
    {
        if (_serviceConnection == null)
        {
            throw new AzureSignalRNotConnectedException();
        }

        return _serviceConnection.WriteAsync(serviceMessage);
    }

    public Task<bool> WriteAckableMessageAsync(ServiceMessage seviceMessage, CancellationToken cancellationToken = default)
    {
        if (_serviceConnection == null)
        {
            throw new AzureSignalRNotConnectedException();
        }

        return _serviceConnection.WriteAckableMessageAsync(seviceMessage, cancellationToken);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public IAsyncEnumerable<GroupMember> ListConnectionsInGroupAsync(string groupName, int? top = null, ulong? tracingId = null, CancellationToken token = default)
    {
        if (_serviceConnection == null)
        {
            throw new AzureSignalRNotConnectedException();
        }

        return _serviceConnection.ListConnectionsInGroupAsync(groupName, top, tracingId, token);
    }
}
