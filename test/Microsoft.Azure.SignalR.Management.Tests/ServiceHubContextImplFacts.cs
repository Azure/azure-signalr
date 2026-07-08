// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
        public async Task RefreshAuthAsync_NullOrEmptyConnectionToken_ThrowsArgumentException()
        {
            var hubContext = CreateHubContext();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => hubContext.RefreshAuthAsync(null!, DateTimeOffset.UtcNow));
            Assert.Equal("connectionToken", exception.ParamName);

            exception = await Assert.ThrowsAsync<ArgumentException>(
                () => hubContext.RefreshAuthAsync(string.Empty, DateTimeOffset.UtcNow));
            Assert.Equal("connectionToken", exception.ParamName);
        }

        [Fact]
        public async Task RefreshAuthAsync_NonSuccessStatus_ThrowsAzureSignalRException()
        {
            foreach (var status in new[] { AckStatus.Forbidden, AckStatus.NotFound, AckStatus.InternalServerError })
            {
                _lifetimeManagerMock
                    .Setup(m => m.RefreshAuthAsync(ConnectionToken, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new RefreshConnectionAuthResult(status));
                var hubContext = CreateHubContext();

                await Assert.ThrowsAsync<AzureSignalRException>(
                    () => hubContext.RefreshAuthAsync(ConnectionToken, DateTimeOffset.UtcNow));
            }
        }

        [Fact]
        public async Task RefreshAuthAsync_OkWithoutClaims_ThrowsAzureSignalRException()
        {
            // On success ASRS must return the post-refresh claim set the SDK mints the token from;
            // a missing claim set is a protocol error, not a silent success.
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshConnectionAuthResult(AckStatus.Ok, claims: null));
            var hubContext = CreateHubContext();

            await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.RefreshAuthAsync(ConnectionToken, DateTimeOffset.UtcNow));
        }

        [Fact]
        public async Task RefreshAuthAsync_Ok_MintsTokenForOwningEndpoint_AndComputesLifetime()
        {
            var expireTime = DateTimeOffset.UtcNow.AddMinutes(10);
            var postRefreshClaims = new[] { new Claim("role", "admin") };
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, expireTime, It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshConnectionAuthResult(AckStatus.Ok, postRefreshClaims));

            var providerMock = new Mock<IServiceEndpointProvider>();
            providerMock
                .Setup(p => p.GenerateClientAccessTokenAsync(HubName, It.IsAny<IEnumerable<Claim>>(), It.IsAny<TimeSpan?>()))
                .ReturnsAsync("minted-token");

            var endpoint = new HubServiceEndpoint(HubName, providerMock.Object, new ServiceEndpoint(FakeEndpointUtils.GetFakeConnectionString(1).First()));
            _endpointManagerMock.Setup(m => m.GetEndpoints(HubName)).Returns(new List<HubServiceEndpoint> { endpoint });
            _endpointManagerMock.Setup(m => m.GetEndpointProvider(It.IsAny<ServiceEndpoint>())).Returns(providerMock.Object);

            var hubContext = CreateHubContext();

            var result = await hubContext.RefreshAuthAsync(ConnectionToken, expireTime, postRefreshClaims);

            Assert.Equal("minted-token", result.AccessToken);
            // min(AccessTokenLifetime, expireTime - now) -> capped by the ~10 min expireTime here.
            Assert.InRange(result.TokenLifetimeSeconds, 1, 600);
            providerMock.Verify(p => p.GenerateClientAccessTokenAsync(HubName, It.IsAny<IEnumerable<Claim>>(), It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task RefreshAuthAsync_Ok_NoServiceEndpoint_ThrowsAzureSignalRException()
        {
            _lifetimeManagerMock
                .Setup(m => m.RefreshAuthAsync(ConnectionToken, It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Claim>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RefreshConnectionAuthResult(AckStatus.Ok, new[] { new Claim("role", "admin") }));
            _endpointManagerMock.Setup(m => m.GetEndpoints(HubName)).Returns(new List<HubServiceEndpoint>());
            var hubContext = CreateHubContext();

            await Assert.ThrowsAsync<AzureSignalRException>(
                () => hubContext.RefreshAuthAsync(ConnectionToken, DateTimeOffset.UtcNow.AddMinutes(10)));
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
