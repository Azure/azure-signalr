// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceHubContextImplFacts
    {
        private const string HubName = "chat";
        private const string ConnectionToken = "conn-token-1";

        private readonly Mock<IServiceHubLifetimeManager> _lifetimeManagerMock = new();
        private readonly Mock<IServiceEndpointManager> _endpointManagerMock = new();

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_NullOrEmptyConnectionToken_ThrowsArgumentException()
        {
            var hubContext = CreateHubContext();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(null!, DateTimeOffset.UtcNow));
            Assert.Equal("connectionToken", exception.ParamName);

            exception = await Assert.ThrowsAsync<ArgumentException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(string.Empty, DateTimeOffset.UtcNow));
            Assert.Equal("connectionToken", exception.ParamName);
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_NonFutureExpireTime_ThrowsArgumentOutOfRangeException()
        {
            var hubContext = CreateHubContext();

            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken, DateTimeOffset.UtcNow.AddSeconds(-1)));

            Assert.Equal("expireTime", exception.ParamName);
            _lifetimeManagerMock.Verify(
                m => m.RefreshAuthAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_NullExpireTime_UsesMaxValueSentinel()
        {
            var claims = new[] { new Claim("role", "admin") };
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, DateTimeOffset.MaxValue, It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshAuthResult(AckStatus.InternalServerError));
            var hubContext = CreateHubContext();

            await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken, claims: claims));

            _lifetimeManagerMock.Verify(
                m => m.RefreshAuthAsync(ConnectionToken, DateTimeOffset.MaxValue, claims, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_NullExpireTimeAndClaims_UsesMaxValueSentinel()
        {
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, DateTimeOffset.MaxValue, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshAuthResult(AckStatus.InternalServerError));
            var hubContext = CreateHubContext();

            await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken));

            _lifetimeManagerMock.Verify(
                m => m.RefreshAuthAsync(ConnectionToken, DateTimeOffset.MaxValue, null, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_NonSuccessStatus_ThrowsAzureSignalRException()
        {
            foreach (var status in new[] { AckStatus.Forbidden, AckStatus.NotFound, AckStatus.InternalServerError })
            {
                _lifetimeManagerMock
                    .Setup(m => m.RefreshAuthAsync(ConnectionToken, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new RefreshAuthResult(status));
                var hubContext = CreateHubContext();

                await Assert.ThrowsAsync<AzureSignalRException>(
                    () => hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken, DateTimeOffset.UtcNow.AddMinutes(10)));
            }
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_OkWithoutClaims_ThrowsAzureSignalRException()
        {
            // On success ASRS must return the post-refresh claim set the SDK mints the token from;
            // a missing claim set is a protocol error, not a silent success.
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshAuthResult(AckStatus.Ok, claims: null));
            var hubContext = CreateHubContext();

            await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken, DateTimeOffset.UtcNow.AddMinutes(10)));
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_OkWithEmptyClaims_ThrowsAzureSignalRException()
        {
            // An empty claim set (like a null one) can't mint a valid token — it would drop the connect-time asrs.s.* system claims. Reject it instead of minting a corrupt token.
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshAuthResult(AckStatus.Ok, claims: Array.Empty<Claim>()));
            var hubContext = CreateHubContext();

            await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken, DateTimeOffset.UtcNow.AddMinutes(10)));
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_Ok_MintsTokenForOwningEndpoint_AndComputesLifetime()
        {
            var expireTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var postRefreshClaims = new[] { new Claim("role", "admin") };
            var providerMock = new Mock<IServiceEndpointProvider>();
            providerMock
                .Setup(p => p.GenerateClientAccessTokenAsync(HubName, It.IsAny<IEnumerable<Claim>>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync("minted-token");

            var endpoint = new HubServiceEndpoint(HubName, providerMock.Object, new ServiceEndpoint(FakeEndpointUtils.GetFakeConnectionString(1).First()));
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, expireTime, It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshAuthResult(AckStatus.Ok, postRefreshClaims, endpoint));
            _endpointManagerMock.Setup(m => m.GetEndpointProvider(It.IsAny<ServiceEndpoint>())).Returns(providerMock.Object);

            var hubContext = CreateHubContext();

            var result = await hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken, expireTime, postRefreshClaims);

            Assert.Equal("minted-token", result.AccessToken);
            // min(AccessTokenLifetime, expireTime - now) -> capped by the ~10 min expireTime here.
            Assert.InRange(result.TokenLifetimeSeconds, 1, 600);
            providerMock.Verify(p => p.GenerateClientAccessTokenAsync(HubName, It.IsAny<IEnumerable<Claim>>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_ClaimsOnly_UsesPreservedRuntimeExpiration()
        {
            var preservedExpireTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var postRefreshClaims = new[]
            {
                new Claim("role", "admin"),
                new Claim(Constants.ClaimType.AuthExpiresOn, preservedExpireTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            };
            var providerMock = new Mock<IServiceEndpointProvider>();
            TimeSpan? generatedTokenLifetime = null;
            providerMock
                .Setup(p => p.GenerateClientAccessTokenAsync(HubName, postRefreshClaims, It.IsAny<TimeSpan?>()))
                .Callback<string, IEnumerable<Claim>, TimeSpan?>((_, _, lifetime) => generatedTokenLifetime = lifetime)
                .ReturnsAsync("minted-token");

            var endpoint = new HubServiceEndpoint(HubName, providerMock.Object, new ServiceEndpoint(FakeEndpointUtils.GetFakeConnectionString(1).First()));
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, DateTimeOffset.MaxValue, It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshAuthResult(AckStatus.Ok, postRefreshClaims, endpoint));
            _endpointManagerMock.Setup(m => m.GetEndpointProvider(It.IsAny<ServiceEndpoint>())).Returns(providerMock.Object);
            var hubContext = CreateHubContext();

            var result = await hubContext.RefreshConnectionAuthenticationAsync(
                ConnectionToken,
                claims: new[] { new Claim("role", "admin") });

            Assert.Equal("minted-token", result.AccessToken);
            Assert.InRange(result.TokenLifetimeSeconds, 594, 600);
            Assert.NotNull(generatedTokenLifetime);
            Assert.InRange(generatedTokenLifetime.Value.TotalSeconds, 594, 600);
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_ElapsedRuntimeExpiration_ThrowsBeforeMintingToken()
        {
            var postRefreshClaims = new[]
            {
                new Claim("role", "admin"),
                new Claim(
                    Constants.ClaimType.AuthExpiresOn,
                    DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            };
            var providerMock = new Mock<IServiceEndpointProvider>();
            var endpoint = new HubServiceEndpoint(HubName, providerMock.Object, new ServiceEndpoint(FakeEndpointUtils.GetFakeConnectionString(1).First()));
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, DateTimeOffset.MaxValue, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshAuthResult(AckStatus.Ok, postRefreshClaims, endpoint));
            var hubContext = CreateHubContext();

            var exception = await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken));

            Assert.Contains("expiration elapsed", exception.Message);
            providerMock.Verify(
                p => p.GenerateClientAccessTokenAsync(It.IsAny<string>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<TimeSpan?>()),
                Times.Never);
        }

        [Fact]
        public async Task RefreshConnectionAuthenticationAsync_OkWithoutOwningEndpoint_ThrowsAzureSignalRException()
        {
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshAuthResult(AckStatus.Ok, new[] { new Claim("role", "admin") }));
            var hubContext = CreateHubContext();

            var exception = await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.RefreshConnectionAuthenticationAsync(ConnectionToken, DateTimeOffset.UtcNow.AddMinutes(10)));
            Assert.Contains("Unexpected refresh result", exception.Message);
        }

        [Fact]
        public async Task GetConnectionClaimsAsync_NullOrEmptyConnectionToken_ThrowsArgumentException()
        {
            var hubContext = CreateHubContext();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => hubContext.GetConnectionClaimsAsync(null!));
            Assert.Equal("connectionToken", exception.ParamName);

            exception = await Assert.ThrowsAsync<ArgumentException>(
                () => hubContext.GetConnectionClaimsAsync(string.Empty));
            Assert.Equal("connectionToken", exception.ParamName);
        }

        [Fact]
        public async Task GetConnectionClaimsAsync_Ok_ReturnsClaims()
        {
            var claims = new[] { new Claim("role", "user"), new Claim("tenant", "contoso") };
            _lifetimeManagerMock
                .Setup(m => m.GetConnectionClaimsAsync(ConnectionToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetConnectionClaimsResult(AckStatus.Ok, claims));
            var hubContext = CreateHubContext();

            var result = await hubContext.GetConnectionClaimsAsync(ConnectionToken);

            Assert.Equal(2, result.Claims.Count);
            Assert.Contains(result.Claims, c => c.Type == "tenant" && c.Value == "contoso");
        }

        [Fact]
        public async Task GetConnectionClaimsAsync_NotFound_ThrowsAzureSignalRException()
        {
            _lifetimeManagerMock
                .Setup(m => m.GetConnectionClaimsAsync(ConnectionToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetConnectionClaimsResult(AckStatus.NotFound));
            var hubContext = CreateHubContext();

            await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.GetConnectionClaimsAsync(ConnectionToken));
        }

        private ServiceHubContext CreateHubContext() =>
            new ServiceHubContextImpl(
                HubName,
                Mock.Of<IHubContext<Hub>>(),
                _lifetimeManagerMock.Object,
                serviceProvider: null,
                negotiateProcessor: null,
                _endpointManagerMock.Object,
                Mock.Of<ILogger<ServiceHubContext>>());
    }
}
