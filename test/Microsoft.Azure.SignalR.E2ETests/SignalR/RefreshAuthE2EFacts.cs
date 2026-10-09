// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task TestRefreshUpdatesConnectedUser()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.AddXunit(_output)
            .AddFilter("Microsoft.Azure.SignalR", LogLevel.Debug)
            .AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
        builder.Services
            .AddSignalR()
            .AddAzureSignalR(options => options.ConnectionString = TestConfiguration.Instance.ConnectionString);

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
        app.MapHub<AuthRefreshHub>(HubPath, options => options.EnableAuthenticationRefresh = true);

        try
        {
            await app.StartAsync();

            var serverUrl = app.Urls.Single();
            var tokenCapture = new ConnectionTokenCaptureHandler();
            await using var connection = new HubConnectionBuilder()
                .ConfigureLogging(logging => logging.AddXunit(_output)
                    .AddFilter("Microsoft.AspNetCore.SignalR.Client", LogLevel.Debug))
                .WithUrl($"{serverUrl}{HubPath}", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(InitialMarker);
                    options.HttpMessageHandlerFactory = handler =>
                    {
                        tokenCapture.InnerHandler = handler;
                        return tokenCapture;
                    };
                })
                .Build();

            _output.WriteLine("Starting the client connection.");
            await StartConnectionAsync(connection);
            var connectionId = connection.ConnectionId;
            var connectionToken = tokenCapture.ConnectionToken;
            Assert.False(string.IsNullOrEmpty(connectionToken));
            _output.WriteLine("Checking the initial connected user.");
            Assert.Equal(InitialMarker, await WaitForMarkerAsync(connection, InitialMarker));

            _output.WriteLine("Sending the authentication refresh request.");
            var (accessToken, tokenLifetimeSeconds) = await RefreshAsync(serverUrl, connectionToken);
            Assert.False(string.IsNullOrEmpty(accessToken));
            Assert.True(tokenLifetimeSeconds > 0);
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

    private static async Task<(string AccessToken, int TokenLifetimeSeconds)> RefreshAsync(string serverUrl, string connectionToken)
    {
        using var timeout = new CancellationTokenSource(OperationTimeout);
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", RefreshedMarker);
        using var response = await httpClient.PostAsync(
            $"{serverUrl}{HubPath}/refresh?id={Uri.EscapeDataString(connectionToken)}",
            content: null,
            cancellationToken: timeout.Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
        return (document.RootElement.GetProperty("accessToken").GetString(),
            document.RootElement.GetProperty("tokenLifetimeSeconds").GetInt32());
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

    private sealed class ConnectionTokenCaptureHandler : DelegatingHandler
    {
        public string ConnectionToken { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode
                && request.RequestUri?.AbsolutePath.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase) == true)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("connectionToken", out var connectionToken))
                {
                    ConnectionToken = connectionToken.GetString();
                }
            }

            return response;
        }
    }
}
#endif
