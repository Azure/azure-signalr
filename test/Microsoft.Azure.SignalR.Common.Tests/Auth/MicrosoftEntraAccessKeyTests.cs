using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

[Collection("Auth")]
public class MicrosoftEntraAccessKeyTests
{
    private const string DefaultSigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const string DefaultToken = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static readonly Uri DefaultEndpoint = new("http://localhost");

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

        await key.UpdateAccessKeyAsync().OrTimeout(TimeSpan.FromSeconds(30));
        var actualLastUpdatedTime = Assert.IsType<DateTime>(lastUpdatedTimeField.GetValue(key));

        if (shouldSkip)
        {
            Assert.Equal(isAuthorized, Assert.IsType<bool>(isAuthorizedField.GetValue(key)));
            Assert.Equal(lastUpdatedTime, actualLastUpdatedTime);
            Assert.Null(key.LastException);
            Assert.False(initializedTcs.Task.IsCompleted);
        }
        else
        {
            Assert.False(Assert.IsType<bool>(isAuthorizedField.GetValue(key)));
            Assert.True(lastUpdatedTime < actualLastUpdatedTime);
            Assert.NotNull(Assert.IsType<InvalidOperationException>(key.LastException));
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
        Assert.Same(exception.InnerException, key.LastException);

        Assert.NotNull(key.LastException);
        var (kid, accessKey) = ("foo", DefaultSigningKey);
        key.UpdateAccessKey(kid, accessKey);
        Assert.Null(key.LastException);
    }

    [Theory]
    [InlineData(DefaultSigningKey)]
    [InlineData("fooooooooooooooooooooooooooooooooobar")]
    public async Task TestUpdateAccessKeySendRequest(string signingKey)
    {
        var httpClientFactory = new TestHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AccessKeyResponse()
            {
                KeyId = "foo",
                AccessKey = signingKey,
            })
        });

        var credential = new TestTokenCredential();
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, credential, httpClientFactory: httpClientFactory);

        await key.UpdateAccessKeyAsync();

        Assert.True(key.IsAuthorized);
        Assert.Equal("foo", key.Id);
        Assert.Equal(signingKey, key.Value);
    }

    [Fact]
    public async Task ThrowUnauthorizedExceptionTest()
    {
        var endpoint = new Uri("https://test-aad-signalr.service.signalr.net");

        var message = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint),
            Content = TextHttpContent.From("")
        };
        var key = new MicrosoftEntraAccessKey(
            endpoint,
            new TestTokenCredential(),
            httpClientFactory: new TestHttpClientFactory(message)
        );

        await key.UpdateAccessKeyAsync();

        Assert.False(key.IsAuthorized);
        var ex = Assert.IsType<AzureSignalRUnauthorizedException>(key.LastException);
        Assert.StartsWith(AzureSignalRUnauthorizedException.ErrorMessage, ex.Message);
        Assert.EndsWith($"Request Uri: {endpoint}", ex.Message);
    }

    [Theory]
    [InlineData(AzureSignalRRuntimeException.NetworkErrorMessage, "403 forbidden, nginx")]
    [InlineData("Please check your role assignments.", "Please check your role assignments.")]
    public async Task ThrowForbiddenExceptionTest(string expected, string responseContent)
    {
        var endpoint = new Uri("https://test-aad-signalr.service.signalr.net");

        var message = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint),
            Content = TextHttpContent.From(responseContent)
        };
        var key = new MicrosoftEntraAccessKey(
            endpoint,
            new TestTokenCredential(),
            httpClientFactory: new TestHttpClientFactory(message)
        );

        await key.UpdateAccessKeyAsync();

        Assert.False(key.IsAuthorized);
        var ex = Assert.IsType<AzureSignalRRuntimeException>(key.LastException);
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Contains(expected, ex.Message);
        Assert.EndsWith($"Request Uri: {endpoint}", ex.Message);
    }

    private sealed class TestHttpClientFactory(HttpResponseMessage message) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new TestHttpClient(message);
        }
    }

    private sealed class TestHttpClient(HttpResponseMessage message) : HttpClient
    {
        public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(DefaultToken, request.Headers.Authorization.ToString()["Bearer ".Length..]);
            return Task.FromResult(message);
        }
    }

    private sealed class TextHttpContent : HttpContent
    {
        private readonly string _content;

        private TextHttpContent(string content) => _content = content;

        internal static HttpContent From(string content) => new TextHttpContent(content);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            return stream.WriteAsync(Encoding.UTF8.GetBytes(_content)).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
    }

    private sealed class TestTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new AccessToken(DefaultToken, DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(1)));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(GetToken(requestContext, cancellationToken));
        }
    }
}
