// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
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
                expireTime,
                CreateClaims(UserId, "admin", refreshedMarker)).OrTimeout();

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
                DateTimeOffset.UtcNow.AddMinutes(10),
                claims: null).OrTimeout();

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
                    DateTimeOffset.UtcNow.AddMinutes(10),
                    CreateClaims("bob", "admin", $"rejected-{Guid.NewGuid():N}")).OrTimeout());

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
                    DateTimeOffset.UtcNow.AddMinutes(10),
                    CreateClaims(UserId, "user", "unknown")).OrTimeout());

            Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await hubContext.DisposeAsync();
        }
    }

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
