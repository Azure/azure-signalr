// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

internal class ServiceConnectionManager<THub> : IDisposable, IServiceConnectionManager<THub> where THub : Hub
{
    private IServiceConnectionContainer _serviceConnection = null;

    public void SetServiceConnection(IServiceConnectionContainer serviceConnection)
    {
        _serviceConnection = serviceConnection;
    }

    public Task StartAsync()
    {
        return _serviceConnection.StartAsync();
    }

    public Task StopAsync()
    {
        return _serviceConnection.StopAsync();
    }

    public async Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token)
    {
        await _serviceConnection.OfflineAsync(mode, token);
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
}
