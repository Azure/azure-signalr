// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;

using Xunit;

#if NET11_0_OR_GREATER
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
#endif

namespace Microsoft.Azure.SignalR.Management.Tests;

public class RefreshAuthE2ETests
{
    private const string HubName = "ManagementAuthRefreshTestHub";
    private const string UserId = "alice";
    private const string MarkerClaimType = "marker";

    public static IEnumerable<object[]> TransportTypes =>
    [
        [ServiceTransportType.Transient],
        [ServiceTransportType.Persistent],
    ];

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TransportTypes))]
    public async Task TestRefreshConnectionAuthentication(ServiceTransportType serviceTransportType)
    {
        var initialMarker = $"initial-{Guid.NewGuid():N}";
        var refreshedMarker = $"refreshed-{Guid.NewGuid():N}";
        using var serviceManager = GenerateServiceManager(serviceTransportType);
        var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
        var tokenCapture = new ConnectionTokenCaptureHandler();
        var negotiation = await hubContext.NegotiateAsync(
            new NegotiationOptions
            {
                UserId = UserId,
                Claims = CreateClaims(UserId, "user", initialMarker),
            },
            default).OrTimeout();
        var connection = CreateHubConnection(
            negotiation.Url,
            negotiation.AccessToken,
            tokenCapture);

        try
        {
            await connection.StartAsync().OrTimeout();
            var connectionId = connection.ConnectionId;
            var connectionToken = await tokenCapture.ConnectionToken.OrTimeout();

            var initialClaims = await hubContext.GetConnectionClaimsAsync(connectionToken).OrTimeout();
            Assert.Contains(initialClaims.Claims, c => c.Type == MarkerClaimType && c.Value == initialMarker);

            var expireTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var refreshResult = await hubContext.RefreshConnectionAuthenticationAsync(
                connectionToken,
                new RefreshConnectionAuthenticationOptions
                {
                    AuthenticationExpiresOn = expireTime,
                    Claims = CreateClaims(UserId, "admin", refreshedMarker),
                }).OrTimeout();

            Assert.False(string.IsNullOrEmpty(refreshResult.AccessToken));
            Assert.InRange(refreshResult.TokenLifetimeSeconds, 1, 600);
            Assert.Equal(connectionId, connection.ConnectionId);

            var tokenClaims = new JwtSecurityTokenHandler().ReadJwtToken(refreshResult.AccessToken).Claims;
            Assert.Contains(tokenClaims, c => c.Type == MarkerClaimType && c.Value == refreshedMarker);

            var refreshedClaims = await hubContext.GetConnectionClaimsAsync(connectionToken).OrTimeout();
            Assert.Contains(refreshedClaims.Claims, c => c.Type == MarkerClaimType && c.Value == refreshedMarker);
            Assert.Contains(refreshedClaims.Claims, c => c.Type == ClaimTypes.Role && c.Value == "admin");

            var method = nameof(TestRefreshConnectionAuthentication);
            var message = Guid.NewGuid().ToString("N");
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.On<string>(method, value => received.TrySetResult(value));

            await hubContext.Clients.Client(connectionId).SendAsync(method, message).OrTimeout();
            Assert.Equal(message, await received.Task.OrTimeout());
            Assert.Equal(connectionId, connection.ConnectionId);
        }
        finally
        {
            try
            {
                await connection.DisposeAsync();
            }
            finally
            {
                await hubContext.DisposeAsync();
            }
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TransportTypes))]
    public async Task TestRefreshConnectionAuthenticationWithoutClaims(ServiceTransportType serviceTransportType)
    {
        var initialMarker = $"initial-{Guid.NewGuid():N}";
        using var serviceManager = GenerateServiceManager(serviceTransportType);
        var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
        var tokenCapture = new ConnectionTokenCaptureHandler();
        var negotiation = await hubContext.NegotiateAsync(
            new NegotiationOptions
            {
                UserId = UserId,
                Claims = CreateClaims(UserId, "user", initialMarker),
            },
            default).OrTimeout();
        var connection = CreateHubConnection(
            negotiation.Url,
            negotiation.AccessToken,
            tokenCapture);

        try
        {
            await connection.StartAsync().OrTimeout();
            var connectionId = connection.ConnectionId;
            var connectionToken = await tokenCapture.ConnectionToken.OrTimeout();

            var refreshResult = await hubContext.RefreshConnectionAuthenticationAsync(
                connectionToken,
                new RefreshConnectionAuthenticationOptions
                {
                    AuthenticationExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10),
                }).OrTimeout();

            Assert.False(string.IsNullOrEmpty(refreshResult.AccessToken));
            Assert.InRange(refreshResult.TokenLifetimeSeconds, 1, 600);
            Assert.Equal(connectionId, connection.ConnectionId);

            var claims = await hubContext.GetConnectionClaimsAsync(connectionToken).OrTimeout();
            Assert.Contains(claims.Claims, c => c.Type == MarkerClaimType && c.Value == initialMarker);
        }
        finally
        {
            try
            {
                await connection.DisposeAsync();
            }
            finally
            {
                await hubContext.DisposeAsync();
            }
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TransportTypes))]
    public async Task TestRefreshConnectionAuthenticationRejectsDifferentUser(ServiceTransportType serviceTransportType)
    {
        var initialMarker = $"initial-{Guid.NewGuid():N}";
        using var serviceManager = GenerateServiceManager(serviceTransportType);
        var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
        var tokenCapture = new ConnectionTokenCaptureHandler();
        var negotiation = await hubContext.NegotiateAsync(
            new NegotiationOptions
            {
                UserId = UserId,
                Claims = CreateClaims(UserId, "user", initialMarker),
            },
            default).OrTimeout();
        var connection = CreateHubConnection(
            negotiation.Url,
            negotiation.AccessToken,
            tokenCapture);

        try
        {
            await connection.StartAsync().OrTimeout();
            var connectionId = connection.ConnectionId;
            var connectionToken = await tokenCapture.ConnectionToken.OrTimeout();

            var exception = await Assert.ThrowsAsync<AzureSignalRException>(() =>
                hubContext.RefreshConnectionAuthenticationAsync(
                    connectionToken,
                    new RefreshConnectionAuthenticationOptions
                    {
                        AuthenticationExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10),
                        Claims = CreateClaims("bob", "admin", $"rejected-{Guid.NewGuid():N}"),
                    }).OrTimeout());

            Assert.Contains("different user", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(connectionId, connection.ConnectionId);

            var claims = await hubContext.GetConnectionClaimsAsync(connectionToken).OrTimeout();
            Assert.Contains(claims.Claims, c => c.Type == MarkerClaimType && c.Value == initialMarker);
        }
        finally
        {
            try
            {
                await connection.DisposeAsync();
            }
            finally
            {
                await hubContext.DisposeAsync();
            }
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TransportTypes))]
    public async Task TestRefreshConnectionAuthenticationForUnknownConnection(ServiceTransportType serviceTransportType)
    {
        using var serviceManager = GenerateServiceManager(serviceTransportType);
        var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        try
        {
            var exception = await Assert.ThrowsAsync<AzureSignalRException>(() =>
                hubContext.RefreshConnectionAuthenticationAsync(
                    $"unknown-{Guid.NewGuid():N}",
                    new RefreshConnectionAuthenticationOptions
                    {
                        AuthenticationExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10),
                        Claims = CreateClaims(UserId, "user", "unknown"),
                    }).OrTimeout());

            Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await hubContext.DisposeAsync();
        }
    }

#if NET11_0_OR_GREATER
    public static IEnumerable<object[]> ClientRefreshScenarios
    {
        get
        {
            foreach (var serviceTransport in new[] { ServiceTransportType.Transient, ServiceTransportType.Persistent })
            {
                foreach (var clientTransport in new[]
                {
                    HttpTransportType.WebSockets,
                    HttpTransportType.ServerSentEvents,
                    HttpTransportType.LongPolling,
                })
                {
                    yield return [serviceTransport, clientTransport, false];
                    yield return [serviceTransport, clientTransport, true];
                }
            }
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(ClientRefreshScenarios))]
    public async Task TestClientRefreshThroughApplicationEndpoints(
        ServiceTransportType serviceTransport,
        HttpTransportType clientTransport,
        bool automatic)
    {
        var hubName = $"managementrefresh{Guid.NewGuid():N}";
        var initialMarker = $"initial-{Guid.NewGuid():N}";
        var refreshedMarker = $"refreshed-{Guid.NewGuid():N}";
        using var serviceManager = GenerateServiceManager(serviceTransport);
        await using var hubContext = await serviceManager.CreateHubContextAsync(hubName, default).OrTimeout();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var refreshed = new TaskCompletionSource<TimeSpan?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshedTransportStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshedTransportRequest = new TaskCompletionSource<HttpStatusCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        string refreshedConnectionToken = null;
        app.MapPost("/chat/negotiate", async (HttpContext context) =>
        {
            if (context.Request.Headers.Authorization != $"Bearer {initialMarker}")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            var result = await hubContext.NegotiateWithTokenLifetimeAsync(new NegotiationOptions
            {
                UserId = UserId,
                Claims = CreateClaims(UserId, "user", initialMarker),
                TokenLifetime = TimeSpan.FromSeconds(20),
                AuthenticationExpiresOn = DateTimeOffset.UtcNow.AddMinutes(2),
                CloseOnAuthenticationExpiration = true,
            }, context.RequestAborted);
            await context.Response.WriteAsJsonAsync(new
            {
                url = result.Url,
                accessToken = result.AccessToken,
                tokenLifetimeSeconds = result.TokenLifetimeSeconds,
            }, context.RequestAborted);
        });
        app.MapPost("/chat/refresh", async (HttpContext context) =>
        {
            if (context.Request.Headers.Authorization != $"Bearer {refreshedMarker}")
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            var connectionToken = context.Request.Headers["X-SignalR-Connection-Token"].ToString();
            if (string.IsNullOrEmpty(connectionToken))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var result = await hubContext.RefreshConnectionAuthenticationAsync(connectionToken,
                new RefreshConnectionAuthenticationOptions
                {
                    Claims = CreateClaims(UserId, "admin", refreshedMarker),
                    AuthenticationExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10),
                    TokenLifetime = TimeSpan.FromMinutes(5),
                }, context.RequestAborted);
            Volatile.Write(ref refreshedConnectionToken, connectionToken);
            await context.Response.WriteAsJsonAsync(new
            {
                accessToken = result.AccessToken,
                tokenLifetimeSeconds = result.TokenLifetimeSeconds,
            }, context.RequestAborted);
        });

        try
        {
            await app.StartAsync().OrTimeout();
            var refreshCredential = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var refreshRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            HubConnection connection = null;
            await using var connectionLifetime = connection = new HubConnectionBuilder()
                .WithUrl($"{app.Urls.Single()}/chat", clientTransport, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        if (connection?.State != HubConnectionState.Connected)
                        {
                            return initialMarker;
                        }
                        refreshRequested.TrySetResult(true);
                        return await refreshCredential.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    };
                    options.HttpMessageHandlerFactory = innerHandler =>
                        new RefreshedTransportCaptureHandler(
                            innerHandler, refreshedMarker, refreshedTransportStarted, refreshedTransportRequest);
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
            var group = $"group-{Guid.NewGuid():N}";
            var messages = System.Threading.Channels.Channel.CreateUnbounded<string>();
            using var subscription = connection.On<string>("message", value => messages.Writer.TryWrite(value));
            string connectionId;
            try
            {
                await connection.StartAsync().OrTimeout();
                connectionId = connection.ConnectionId;
                Assert.False(string.IsNullOrEmpty(connectionId));
                await hubContext.Groups.AddToGroupAsync(connectionId, group).OrTimeout();
                if (automatic)
                {
                    await refreshRequested.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.False(refreshed.Task.IsCompleted);
                }
                await hubContext.Clients.Client(connectionId).SendAsync("message", "before-refresh").OrTimeout();
                Assert.Equal("before-refresh", await messages.Reader.ReadAsync().AsTask().OrTimeout());
                refreshCredential.SetResult(refreshedMarker);
            }
            finally
            {
                refreshCredential.TrySetCanceled();
            }
            var lifetime = automatic
                ? await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(30))
                : await connection.RefreshAuthenticationAsync().OrTimeout();
            Assert.True(lifetime > TimeSpan.Zero);
            var claims = await hubContext.GetConnectionClaimsAsync(
                Volatile.Read(ref refreshedConnectionToken)).OrTimeout();
            Assert.Contains(claims.Claims, claim => claim.Type == MarkerClaimType && claim.Value == refreshedMarker);
            Assert.Contains(claims.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "admin");
            await hubContext.Clients.User(UserId).SendAsync("message", "user-after-refresh").OrTimeout();
            Assert.Equal("user-after-refresh", await messages.Reader.ReadAsync().AsTask().OrTimeout());
            if (clientTransport == HttpTransportType.LongPolling)
            {
                Assert.True(await refreshedTransportStarted.Task.OrTimeout());
                await hubContext.Clients.Client(connectionId).SendAsync("message", "new-token-poll").OrTimeout();
                Assert.Equal(HttpStatusCode.OK, await refreshedTransportRequest.Task.OrTimeout());
                Assert.Equal("new-token-poll", await messages.Reader.ReadAsync().AsTask().OrTimeout());
            }
            await hubContext.Clients.Group(group).SendAsync("message", "group-after-refresh").OrTimeout();
            Assert.Equal("group-after-refresh", await messages.Reader.ReadAsync().AsTask().OrTimeout());
            Assert.Equal(connectionId, connection.ConnectionId);
            Assert.Equal(HubConnectionState.Connected, connection.State);
        }
        finally
        {
            await app.StopAsync().OrTimeout();
        }
    }

    private sealed class RefreshedTransportCaptureHandler(
        HttpMessageHandler innerHandler,
        string marker,
        TaskCompletionSource<bool> refreshedRequestStarted,
        TaskCompletionSource<HttpStatusCode> refreshedRequest) : DelegatingHandler(innerHandler)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = request.Headers.Authorization?.Parameter;
            var reader = new JwtSecurityTokenHandler();
            var usesRefreshedToken = request.Method == HttpMethod.Get && token != null && reader.CanReadToken(token)
                && reader.ReadJwtToken(token).Claims.Any(claim => claim.Type == MarkerClaimType && claim.Value == marker);
            if (usesRefreshedToken)
            {
                refreshedRequestStarted.TrySetResult(true);
            }
            var response = await base.SendAsync(request, cancellationToken);
            if (usesRefreshedToken)
            {
                refreshedRequest.TrySetResult(response.StatusCode);
            }
            return response;
        }
    }
#endif

    private static List<Claim> CreateClaims(string userId, string role, string marker) =>
    [
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(ClaimTypes.Role, role),
        new Claim(MarkerClaimType, marker),
    ];

    private static ServiceManager GenerateServiceManager(ServiceTransportType serviceTransportType) =>
        new ServiceManagerBuilder()
            .WithOptions(options =>
            {
                options.ConnectionString = TestConfiguration.Instance.ConnectionString;
                options.ServiceTransportType = serviceTransportType;
            })
            .WithCallingAssembly()
            .BuildServiceManager();

    private static HubConnection CreateHubConnection(
        string endpoint,
        string accessToken,
        ConnectionTokenCaptureHandler tokenCapture) =>
        new HubConnectionBuilder()
            .WithUrl(endpoint, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(accessToken);
                options.HttpMessageHandlerFactory = innerHandler =>
                {
                    tokenCapture.InnerHandler = innerHandler;
                    return tokenCapture;
                };
            })
            .Build();

    private sealed class ConnectionTokenCaptureHandler : DelegatingHandler
    {
        private readonly TaskCompletionSource<string> _connectionToken =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> ConnectionToken => _connectionToken.Task;

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
                    _connectionToken.TrySetResult(connectionToken.GetString());
                }
            }

            return response;
        }
    }
}
