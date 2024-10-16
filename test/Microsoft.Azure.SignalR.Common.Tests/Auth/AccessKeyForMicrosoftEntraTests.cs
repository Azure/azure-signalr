using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

[Collection("Auth")]
public class AccessKeyForMicrosoftEntraTests
{
    private const string DefaultSigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static Uri DefaultEndpoint = new Uri("http://localhost");

    [Theory]
    [InlineData("https://a.bc", "https://a.bc/api/v1/auth/accessKey")]
    [InlineData("https://a.bc:80", "https://a.bc:80/api/v1/auth/accessKey")]
    [InlineData("https://a.bc:443", "https://a.bc/api/v1/auth/accessKey")]
    public void TestExpectedGetAccessKeyUrl(string endpoint, string expectedGetAccessKeyUrl)
    {
        var key = new MicrosoftEntraAccessKey(new Uri(endpoint), new DefaultAzureCredential());
        Assert.Equal(expectedGetAccessKeyUrl, key.GetAccessKeyUrl);
    }

    [Fact]
    public async Task TestUpdateAccessKey()
    {
        var mockCredential = new Mock<TokenCredential>();
        mockCredential.Setup(credential => credential.GetTokenAsync(
            It.IsAny<TokenRequestContext>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Mock GetTokenAsync throws an exception"));
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object);

        var audience = "http://localhost/chat";
        var claims = Array.Empty<Claim>();
        var lifetime = TimeSpan.FromHours(1);
        var algorithm = AccessTokenAlgorithm.HS256;

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm, cts.Token)
        );

        var (kid, accessKey) = ("foo", DefaultSigningKey);
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
    public async Task TestUpdateAccessKeyAsyncShouldSkip(bool isAuthorized, int timeElapsed, bool shouldSkip)
    {
        var mockCredential = new Mock<TokenCredential>();
        mockCredential.Setup(credential => credential.GetTokenAsync(
            It.IsAny<TokenRequestContext>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Mock GetTokenAsync throws an exception"));
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object);
        var isAuthorizedField = typeof(MicrosoftEntraAccessKey).GetField("_isAuthorized", BindingFlags.NonPublic | BindingFlags.Instance);
        isAuthorizedField.SetValue(key, isAuthorized);
        Assert.Equal(isAuthorized, (bool)isAuthorizedField.GetValue(key));

        var lastUpdatedTime = DateTime.UtcNow - TimeSpan.FromMinutes(timeElapsed);
        var lastUpdatedTimeField = typeof(MicrosoftEntraAccessKey).GetField("_lastUpdatedTime", BindingFlags.NonPublic | BindingFlags.Instance);
        lastUpdatedTimeField.SetValue(key, lastUpdatedTime);

        var initializedTcsField = typeof(MicrosoftEntraAccessKey).GetField("_initializedTcs", BindingFlags.NonPublic | BindingFlags.Instance);
        var initializedTcs = (TaskCompletionSource<object>)initializedTcsField.GetValue(key);

        var lastExceptionFields = typeof(MicrosoftEntraAccessKey).GetField("_lastException", BindingFlags.NonPublic | BindingFlags.Instance);

        await key.UpdateAccessKeyAsync().OrTimeout(TimeSpan.FromSeconds(30));
        var actualLastUpdatedTime = Assert.IsType<DateTime>(lastUpdatedTimeField.GetValue(key));

        if (shouldSkip)
        {
            Assert.Equal(isAuthorized, Assert.IsType<bool>(isAuthorizedField.GetValue(key)));
            Assert.Equal(lastUpdatedTime, actualLastUpdatedTime);
            Assert.Null(lastExceptionFields.GetValue(key));
            Assert.False(initializedTcs.Task.IsCompleted);
        }
        else
        {
            Assert.False(Assert.IsType<bool>(isAuthorizedField.GetValue(key)));
            Assert.True(lastUpdatedTime < actualLastUpdatedTime);
            Assert.NotNull(Assert.IsType<InvalidOperationException>(lastExceptionFields.GetValue(key)));
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
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object);

        var audience = "http://localhost/chat";
        var claims = Array.Empty<Claim>();
        var lifetime = TimeSpan.FromHours(1);
        var algorithm = AccessTokenAlgorithm.HS256;

        await key.UpdateAccessKeyAsync();

        var exception = await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(
            async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm)
        );
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task TestUpdateAccessKeyAfterInitializeFailed()
    {
        var mockCredential = new Mock<TokenCredential>();
        mockCredential.Setup(credential => credential.GetTokenAsync(
            It.IsAny<TokenRequestContext>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Mock GetTokenAsync throws an exception"));
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object);

        var audience = "http://localhost/chat";
        var claims = Array.Empty<Claim>();
        var lifetime = TimeSpan.FromHours(1);
        var algorithm = AccessTokenAlgorithm.HS256;

        await key.UpdateAccessKeyAsync();

        var exception = await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(
            async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm)
        );
        Assert.IsType<InvalidOperationException>(exception.InnerException);

        var lastExceptionFields = typeof(MicrosoftEntraAccessKey).GetField("_lastException", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(lastExceptionFields.GetValue(key));
        var (kid, accessKey) = ("foo", DefaultSigningKey);
        key.UpdateAccessKey(kid, accessKey);
        Assert.Null(lastExceptionFields.GetValue(key));
    }
}
