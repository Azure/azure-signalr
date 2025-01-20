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
    public IServiceConnectionContainer ServiceConnectionContainer { get; private set; }

    public void SetServiceConnection(IServiceConnectionContainer serviceConnection)
    {
        ServiceConnectionContainer = serviceConnection;
    }

    public Task StartAsync()
    {
        return ServiceConnectionContainer.StartAsync();
    }

    public Task StopAsync()
    {
        return ServiceConnectionContainer.StopAsync();
    }

    public async Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token)
    {
        await ServiceConnectionContainer.OfflineAsync(mode, token);
    }

    public async Task CloseClientConnections(CancellationToken cancellationToken)
    {
        await ServiceConnectionContainer.CloseClientConnections(cancellationToken);
    }

    public Task WriteAsync(ServiceMessage serviceMessage)
    {
        if (ServiceConnectionContainer == null)
        {
            throw new AzureSignalRNotConnectedException();
        }
        return ServiceConnectionContainer.WriteAsync(serviceMessage);
    }

    public Task<bool> WriteAckableMessageAsync(ServiceMessage seviceMessage, CancellationToken cancellationToken = default)
    {
        if (ServiceConnectionContainer == null)
        {
            throw new AzureSignalRNotConnectedException();
        }
        return ServiceConnectionContainer.WriteAckableMessageAsync(seviceMessage, cancellationToken);
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }
}
