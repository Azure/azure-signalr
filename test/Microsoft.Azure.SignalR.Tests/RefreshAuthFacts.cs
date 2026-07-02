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
using Microsoft.Azure.SignalR.Tests.Common;

using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class RefreshAuthFacts
{
    private sealed class TestHub : Hub
    {
    }

    [Fact]
    public async Task ServiceConnection_UpdateConnectionClaims_UpdatesLiveConnectionUser()
    {
        var connectionId = Guid.NewGuid().ToString("N");

        var proxy = new ServiceConnectionProxy();
        var serverTask = proxy.WaitForServerConnectionAsync(1);
        _ = proxy.StartAsync();
        await serverTask.OrTimeout();

        // Open a live client connection with an initial claim.
        var connectionTask = proxy.WaitForConnectionAsync(connectionId);
        await proxy.WriteMessageAsync(new OpenConnectionMessage(connectionId, new[] { new Claim("my-claim", "original") }));
        var connection = (ClientConnectionContext)await connectionTask.OrTimeout();

        Assert.Equal("original", connection.User.FindFirst("my-claim")?.Value);

        // The service pushes refreshed claims for the live client connection.
        await proxy.WriteMessageAsync(new UpdateConnectionClaimsMessage(connectionId, new[] { new Claim("my-claim", "updated") }));

        // The message is dispatched asynchronously; poll until the user is updated.
        var updated = false;
        for (var i = 0; i < 100 && !updated; i++)
        {
            if (connection.User?.FindFirst("my-claim")?.Value == "updated")
            {
                updated = true;
                break;
            }
            await Task.Delay(20);
        }

        Assert.True(updated, "The client connection user was not updated by UpdateConnectionClaimsMessage.");

        proxy.Stop();
    }

    [Fact]
    public async Task ServiceConnection_UpdateConnectionClaims_ForUnknownConnection_IsIgnored()
    {
        var proxy = new ServiceConnectionProxy();
        var serverTask = proxy.WaitForServerConnectionAsync(1);
        _ = proxy.StartAsync();
        await serverTask.OrTimeout();

        // Dispatching for a non-existent connection should be a no-op and must not throw.
        await proxy.WriteMessageAsync(new UpdateConnectionClaimsMessage(Guid.NewGuid().ToString("N"), new[] { new Claim("my-claim", "updated") }));

        Assert.Empty(proxy.ClientConnectionManager.ClientConnections);

        proxy.Stop();
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
        var result = await manager.RefreshConnectionAuthAsync(message);

        Assert.Equal(expected, result.Status);
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

        public Task<RefreshConnectionAuthResult> RefreshConnectionAuthAsync(RefreshAuthMessage message, CancellationToken cancellationToken = default)
        {
            LastRefreshMessage = message;
            return Task.FromResult(new RefreshConnectionAuthResult(RefreshResult));
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
