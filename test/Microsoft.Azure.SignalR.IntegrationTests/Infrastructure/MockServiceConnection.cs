// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;

/// <summary>
/// Encapsulates the actual ServiceConnection to facilitate sync up of MockService and SDK connections
/// </summary>
internal class MockServiceConnection : IServiceConnection
{
    private static int Number = 0;

    private readonly IMockService _mockService;

    public int ConnectionNumber { get; private set; }

    public IServiceConnection InnerServiceConnection { get; }

    public MockServiceConnectionContext MyConnectionContext { get; set; }

    public ServiceConnectionStatus Status => InnerServiceConnection.Status;

    public Task ConnectionInitializedTask => InnerServiceConnection.ConnectionInitializedTask;

    public Task ConnectionOfflineTask => InnerServiceConnection.ConnectionOfflineTask;

    public string ConnectionId => InnerServiceConnection.ConnectionId;

    public string ServerId => InnerServiceConnection.ServerId;

    internal MockServiceConnection(IMockService mockService, IServiceConnection serviceConnection)
    {
        _mockService = mockService;
        InnerServiceConnection = serviceConnection;
        ConnectionNumber = Interlocked.Increment(ref Number);
        _mockService.RegisterSDKConnection(this);
    }

    public event Action<StatusChange> ConnectionStatusChanged
    {
        add => InnerServiceConnection.ConnectionStatusChanged += value;
        remove => InnerServiceConnection.ConnectionStatusChanged -= value;
    }

    public Task StartAsync(string target = null)
    {
        var tag = $"svc_{ConnectionNumber}_";
        target = tag + target;
        return InnerServiceConnection.StartAsync(target);
    }

    public Task StopAsync() => InnerServiceConnection.StopAsync();

    public Task WriteAsync(ServiceMessage serviceMessage) => InnerServiceConnection.WriteAsync(serviceMessage);

    public async Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
    {
        await WriteAsync(serviceMessage);
        return true;
    }

    public bool TryAddClientConnection(IClientConnection connection)
    {
        return true;
    }

    public bool TryRemoveClientConnection(string connectionId, out IClientConnection connection)
    {
        connection = null;
        return true;
    }
}
