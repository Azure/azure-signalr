using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    [Collection("Auth")]
    public class AccessKeyForMicrosoftEntraTests
    {
        private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [Theory]
        [InlineData("https://a.bc", "https://a.bc/api/v1/auth/accessKey")]
        [InlineData("https://a.bc:80", "https://a.bc:80/api/v1/auth/accessKey")]
        [InlineData("https://a.bc:443", "https://a.bc/api/v1/auth/accessKey")]
        public void TestConstructor(string endpoint, string expectedAuthorizeUrl)
        {
            var key = new AccessKeyForMicrosoftEntra(new Uri(endpoint), new DefaultAzureCredential());
            Assert.Equal(expectedAuthorizeUrl, key.GetAccessKeyUrl);
        }

        [Fact]
        public async Task TestUpdateAccessKey()
        {
            var mockCredential = new Mock<TokenCredential>();
            mockCredential.Setup(credential => credential.GetTokenAsync(
                It.IsAny<TokenRequestContext>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Mock GetTokenAsync throws an exception"));
            var key = new AccessKeyForMicrosoftEntra(new Uri("http://localhost"), mockCredential.Object);

            var audience = "http://localhost/chat";
            var claims = Array.Empty<Claim>();
            var lifetime = TimeSpan.FromHours(1);
            var algorithm = AccessTokenAlgorithm.HS256;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm, cts.Token)
            );

            var (kid, accessKey) = ("foo", SigningKey);
            key.UpdateAccessKey(kid, accessKey);

            var token = await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm);
            Assert.NotNull(token);
        }

        [Theory]
        [InlineData(false, 1, true)]
        [InlineData(false, 4, true)]
        [InlineData(false, 6, false)]
        [InlineData(true, 6, true)]
        [InlineData(true, 54, true)]
        [InlineData(true, 56, false)]
        public async Task TestUpdateAccessKeyShouldSkip(bool isAuthorized, int timeElapsed, bool shouldSkip)
        {
            var mockCredential = new Mock<TokenCredential>();
            mockCredential.Setup(credential => credential.GetTokenAsync(
                It.IsAny<TokenRequestContext>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Mock GetTokenAsync throws an exception"));
            var key = new AccessKeyForMicrosoftEntra(new Uri("http://localhost"), mockCredential.Object);
            var isAuthorizedField = typeof(AccessKeyForMicrosoftEntra).GetField("_isAuthorized", BindingFlags.NonPublic | BindingFlags.Instance);
            isAuthorizedField.SetValue(key, isAuthorized);
            Assert.Equal(isAuthorized, (bool)isAuthorizedField.GetValue(key));

            var lastUpdatedTime = DateTime.UtcNow - TimeSpan.FromMinutes(timeElapsed);
            var lastUpdatedTimeField = typeof(AccessKeyForMicrosoftEntra).GetField("_lastUpdatedTime", BindingFlags.NonPublic | BindingFlags.Instance);
            lastUpdatedTimeField.SetValue(key, lastUpdatedTime);

            var initializedTcsField = typeof(AccessKeyForMicrosoftEntra).GetField("_initializedTcs", BindingFlags.NonPublic | BindingFlags.Instance);
            var initializedTcs = (TaskCompletionSource<object>)initializedTcsField.GetValue(key);

            var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            if (shouldSkip)
            {
                await key.UpdateAccessKeyAsync(source.Token);
                Assert.Equal(isAuthorized, (bool)isAuthorizedField.GetValue(key));
                Assert.Equal(lastUpdatedTime, (DateTime)lastUpdatedTimeField.GetValue(key));
                Assert.False(initializedTcs.Task.IsCompleted);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await key.UpdateAccessKeyAsync(source.Token));
                Assert.False((bool)isAuthorizedField.GetValue(key));
                Assert.True(lastUpdatedTime < (DateTime)lastUpdatedTimeField.GetValue(key));
                Assert.True(initializedTcs.Task.IsCompleted);
            }
        }

        [Fact]
        public async Task TestInitializeFailed()
        {
            var mockCredential = new Mock<TokenCredential>();
            mockCredential.Setup(credential => credential.GetTokenAsync(
                It.IsAny<TokenRequestContext>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Mock GetTokenAsync throws an exception"));
            var key = new AccessKeyForMicrosoftEntra(new Uri("http://localhost"), mockCredential.Object);

            var audience = "http://localhost/chat";
            var claims = Array.Empty<Claim>();
            var lifetime = TimeSpan.FromHours(1);
            var algorithm = AccessTokenAlgorithm.HS256;

            var task = Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(
                async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm)
            );

            var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await key.UpdateAccessKeyAsync(source.Token)
            );

            await task;
        }
    }
}
