// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Azure;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class RefreshAuthFacts
{
    private sealed class TestHub : Hub
    {
    }

    [Fact]
    public async Task ServiceConnectionManager_RefreshConnectionAuth_DelegatesOk()
    {
        await AssertRefreshDelegates(AckStatus.Ok);
    }

    [Fact]
    public async Task ServiceConnectionManager_RefreshConnectionAuth_DelegatesNotFound()
    {
        await AssertRefreshDelegates(AckStatus.NotFound);
    }

    [Fact]
    public async Task ServiceConnectionManager_RefreshConnectionAuth_DelegatesForbidden()
    {
        await AssertRefreshDelegates(AckStatus.Forbidden);
    }

    private static async Task AssertRefreshDelegates(AckStatus expected)
    {
        var container = new FakeServiceConnectionContainer { RefreshResult = expected };
        var manager = new ServiceConnectionManager<TestHub>();
        manager.SetServiceConnection(container);

        var message = new RefreshAuthMessage("connection-token", null, DateTimeOffset.UtcNow.AddMinutes(30), 0);
        var status = await manager.RefreshConnectionAuthAsync(message);

        Assert.Equal(expected, status);
        Assert.Same(message, container.LastRefreshMessage);
    }

    [Fact]
    public async Task ServiceConnectionManager_GetConnectionClaims_DelegatesResult()
    {
        var claims = new List<Claim> { new("role", "admin") };
        var container = new FakeServiceConnectionContainer { ClaimsResult = (AckStatus.Ok, claims) };
        var manager = new ServiceConnectionManager<TestHub>();
        manager.SetServiceConnection(container);

        var (status, returned) = await manager.GetConnectionClaimsAsync(new GetConnectionClaimsMessage("connection-token", 0));

        Assert.Equal(AckStatus.Ok, status);
        Assert.Same(claims, returned);
    }

    [Fact]
    public async Task ServiceConnectionManager_GetConnectionClaims_NotFound_HasNoClaims()
    {
        var container = new FakeServiceConnectionContainer { ClaimsResult = (AckStatus.NotFound, null) };
        var manager = new ServiceConnectionManager<TestHub>();
        manager.SetServiceConnection(container);

        var (status, returned) = await manager.GetConnectionClaimsAsync(new GetConnectionClaimsMessage("connection-token", 0));

        Assert.Equal(AckStatus.NotFound, status);
        Assert.Null(returned);
    }

    [Fact]
    public async Task ServiceConnectionManager_RefreshConnectionAuth_ThrowsWhenNotConnected()
    {
        var manager = new ServiceConnectionManager<TestHub>();
        await Assert.ThrowsAsync<AzureSignalRNotConnectedException>(
            () => manager.RefreshConnectionAuthAsync(new RefreshAuthMessage("connection-token", null, DateTimeOffset.UtcNow, 0)));
    }

    [Fact]
    public async Task ServiceConnectionManager_GetConnectionClaims_ThrowsWhenNotConnected()
    {
        var manager = new ServiceConnectionManager<TestHub>();
        await Assert.ThrowsAsync<AzureSignalRNotConnectedException>(
            () => manager.GetConnectionClaimsAsync(new GetConnectionClaimsMessage("connection-token", 0)));
    }

    private sealed class FakeServiceConnectionContainer : IServiceConnectionContainer
    {
        public AckStatus RefreshResult { get; set; } = AckStatus.Ok;

        public (AckStatus Status, IReadOnlyList<Claim> Claims) ClaimsResult { get; set; } = (AckStatus.Ok, Array.Empty<Claim>());

        public RefreshAuthMessage LastRefreshMessage { get; private set; }

        public Task<AckStatus> RefreshConnectionAuthAsync(RefreshAuthMessage message, CancellationToken cancellationToken = default)
        {
            LastRefreshMessage = message;
            return Task.FromResult(RefreshResult);
        }

        public Task<(AckStatus Status, IReadOnlyList<Claim> Claims)> GetConnectionClaimsAsync(GetConnectionClaimsMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ClaimsResult);
        }

        public ServiceConnectionStatus Status => ServiceConnectionStatus.Connected;

        public Task ConnectionInitializedTask => Task.CompletedTask;

        public string ServersTag => string.Empty;

        public bool HasClients => false;

        public Task StartGetServersPing() => Task.CompletedTask;

        public Task StopGetServersPing() => Task.CompletedTask;

        public Task StartAsync() => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token) => Task.CompletedTask;

        public Task CloseClientConnections(CancellationToken token) => Task.CompletedTask;

        public Task WriteAsync(ServiceMessage serviceMessage) => Task.CompletedTask;

        public Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public IAsyncEnumerable<Page<SignalRGroupMember>> ListConnectionsInGroupAsync(string groupName, int? top = null, int? maxPageSize = null, string continuationToken = null, ulong? tracingId = null, CancellationToken token = default) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
