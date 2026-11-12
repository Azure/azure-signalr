// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests;

public class RefreshAuthE2EFacts
{
    private const string HubPath = "/auth-refresh";
    private const string InitialMarker = "initial";
    private const string MarkerClaimType = "marker";
    private const string RefreshedMarker = "refreshed";
    private const int ConnectionStartAttempts = 20;
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ConnectionStartRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MarkerPollInterval = TimeSpan.FromMilliseconds(50);

    private readonly ITestOutputHelper _output;

    public RefreshAuthE2EFacts(ITestOutputHelper output)
    {
        _output = output;
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.ServerSentEvents)]
    [InlineData(HttpTransportType.LongPolling)]
    public Task TestClientRefreshUpdatesConnectedUser(HttpTransportType transport) =>
        RunRefreshAsync(transport, automatic: false, accept: true);

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.ServerSentEvents)]
    [InlineData(HttpTransportType.LongPolling)]
    public Task TestClientAutoRefreshUpdatesConnectedUser(HttpTransportType transport) =>
        RunRefreshAsync(transport, automatic: true, accept: true);

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(HttpTransportType.WebSockets)]
    [InlineData(HttpTransportType.ServerSentEvents)]
    [InlineData(HttpTransportType.LongPolling)]
    public Task TestClientRejectedRefreshPreservesConnectedUser(HttpTransportType transport) =>
        RunRefreshAsync(transport, automatic: false, accept: false);

    private async Task RunRefreshAsync(HttpTransportType transport, bool automatic, bool accept)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.AddXunit(_output)
            .AddFilter("Microsoft.Azure.SignalR", LogLevel.Debug)
            .AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
        builder.Services
            .AddSignalR()
            .AddAzureSignalR(options =>
            {
                options.ConnectionString = TestConfiguration.Instance.ConnectionString;
                options.AccessTokenLifetime = TimeSpan.FromSeconds(20);
            });

        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var marker = context.Request.Headers.Authorization == $"Bearer {RefreshedMarker}"
                ? RefreshedMarker
                : InitialMarker;
            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "alice"),
                new Claim(MarkerClaimType, marker),
            }, "TestAuth"));
            await next(context);
        });
        app.MapHub<AuthRefreshHub>(HubPath, options =>
        {
            options.EnableAuthenticationRefresh = true;
            options.OnAuthenticationRefresh = _ => Task.FromResult(accept);
        });

        try
        {
            await app.StartAsync();

            var serverUrl = app.Urls.Single();
            var refreshResponse = new TaskCompletionSource<(string AccessToken, int TokenLifetimeSeconds)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshCredential = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshed = new TaskCompletionSource<TimeSpan?>(TaskCreationOptions.RunContinuationsAsynchronously);
            HubConnection connection = null;
            await using var connectionLifetime = connection = new HubConnectionBuilder()
                .ConfigureLogging(logging => logging.AddXunit(_output)
                    .AddFilter("Microsoft.AspNetCore.SignalR.Client", LogLevel.Debug))
                .WithUrl($"{serverUrl}{HubPath}", transport, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        if (connection?.State != HubConnectionState.Connected)
                        {
                            return InitialMarker;
                        }
                        refreshRequested.TrySetResult(true);
                        return await refreshCredential.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    };
                    options.HttpMessageHandlerFactory = handler => new RefreshResponseCaptureHandler(handler, refreshResponse);
                })
                .WithAuthenticationRefresh(options =>
                {
                    options.EnableAutoRefresh = automatic;
                    options.RefreshBeforeExpiration = TimeSpan.FromSeconds(15);
                })
                .Build();
            connection.AuthenticationRefreshed += context =>
            {
                refreshed.TrySetResult(context.NewTokenLifetime);
                return Task.CompletedTask;
            };

            string connectionId;
            try
            {
                _output.WriteLine("Starting the client connection.");
                await StartConnectionAsync(connection);
                connectionId = connection.ConnectionId;
                Assert.False(string.IsNullOrEmpty(connectionId));
                if (automatic)
                {
                    await refreshRequested.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.False(refreshed.Task.IsCompleted);
                    Assert.False(refreshResponse.Task.IsCompleted);
                }
                _output.WriteLine("Checking the initial connected user.");
                Assert.Equal(InitialMarker, await WaitForMarkerAsync(connection, InitialMarker));
                refreshCredential.SetResult(RefreshedMarker);
            }
            finally
            {
                refreshCredential.TrySetCanceled();
            }
            if (!accept)
            {
                using var timeout = new CancellationTokenSource(OperationTimeout);
                var exception = await Assert.ThrowsAsync<HttpRequestException>(
                    () => connection.RefreshAuthenticationAsync(timeout.Token));
                Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
                Assert.Equal(InitialMarker, await GetMarkerAsync(connection, timeout.Token));
                Assert.Equal(HubConnectionState.Connected, connection.State);
                Assert.Equal(connectionId, connection.ConnectionId);
                return;
            }

            _output.WriteLine(automatic ? "Waiting for automatic authentication refresh." : "Refreshing through the client API.");
            TimeSpan? tokenLifetime;
            if (automatic)
            {
                tokenLifetime = await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            else
            {
                using var timeout = new CancellationTokenSource(OperationTimeout);
                tokenLifetime = await connection.RefreshAuthenticationAsync(timeout.Token);
            }
            Assert.True(tokenLifetime > TimeSpan.Zero);
            var (accessToken, tokenLifetimeSeconds) = await refreshResponse.Task.WaitAsync(OperationTimeout);
            Assert.False(string.IsNullOrEmpty(accessToken));
            Assert.True(tokenLifetimeSeconds > 0);
            Assert.Equal(TimeSpan.FromSeconds(tokenLifetimeSeconds), tokenLifetime);
            Assert.Contains(new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims,
                claim => claim.Type == MarkerClaimType && claim.Value == RefreshedMarker);
            _output.WriteLine("Checking the refreshed user on the existing connection.");
            var marker = await WaitForMarkerAsync(connection, RefreshedMarker);

            Assert.Equal(RefreshedMarker, marker);
            Assert.Equal(connectionId, connection.ConnectionId);
        }
        finally
        {
            _output.WriteLine("Stopping the app after disposing the client connection.");
            await app.StopAsync();
        }
    }

    private static Task<string> GetMarkerAsync(HubConnection connection, CancellationToken cancellationToken) =>
        connection.InvokeAsync<string>(nameof(AuthRefreshHub.GetMarker), cancellationToken);

    private static async Task StartConnectionAsync(HubConnection connection)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var timeout = new CancellationTokenSource(OperationTimeout);
            try
            {
                await connection.StartAsync(timeout.Token);
                return;
            }
            catch (HubException) when (attempt < ConnectionStartAttempts - 1)
            {
                await Task.Delay(ConnectionStartRetryDelay);
            }
        }
    }

    private static async Task<string> WaitForMarkerAsync(HubConnection connection, string expected)
    {
        using var timeout = new CancellationTokenSource(OperationTimeout);
        string marker = null;
        try
        {
            while (true)
            {
                marker = await GetMarkerAsync(connection, timeout.Token);
                if (marker == expected)
                {
                    return marker;
                }
                await Task.Delay(MarkerPollInterval, timeout.Token);
            }
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Timed out waiting for marker '{expected}' after {OperationTimeout.TotalSeconds} seconds. " +
                $"Last observed marker: '{marker ?? "<none>"}'. Client state: {connection.State}.", ex);
        }
    }

    private sealed class AuthRefreshHub : Hub
    {
        public string GetMarker() => Context.User?.FindFirst(MarkerClaimType)?.Value;
    }

    private sealed class RefreshResponseCaptureHandler : DelegatingHandler
    {
        private readonly TaskCompletionSource<(string AccessToken, int TokenLifetimeSeconds)> _response;

        public RefreshResponseCaptureHandler(
            HttpMessageHandler innerHandler,
            TaskCompletionSource<(string AccessToken, int TokenLifetimeSeconds)> response)
            : base(innerHandler)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode
                && request.RequestUri?.AbsolutePath.EndsWith("/refresh", StringComparison.OrdinalIgnoreCase) == true)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                _response.TrySetResult((document.RootElement.GetProperty("accessToken").GetString(),
                    document.RootElement.GetProperty("tokenLifetimeSeconds").GetInt32()));
            }

            return response;
        }
    }
}
#endif
