using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
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

        var (kid, accessKey) = ("foo", DefaultSigningKey);
        key.UpdateAccessKey(kid, accessKey);

        var token = await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm);
        Assert.NotNull(token);
    }

    [Theory]
    [InlineData(false, 1, true, false)]
    [InlineData(false, 4, true, false)]
    [InlineData(false, 6, false, true)] // > 5, should try update when unauthorized
    [InlineData(true, 1, true, false)]
    [InlineData(true, 54, true, false)]
    [InlineData(true, 56, true, true)] // > 55, should try update and log the exception
    [InlineData(true, 119, true, true)] // > 55, should try update and log the exception
    [InlineData(true, 121, false, true)] // > 120, should set key unauthorized and log the exception
    public async Task TestUpdateAccessKeyAsyncShouldSkip(bool isAuthorized, int timeElapsed, bool skip, bool hasException)
    {
        var mockCredential = new Mock<TokenCredential>();
        mockCredential.Setup(credential => credential.GetTokenAsync(
            It.IsAny<TokenRequestContext>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Mock GetTokenAsync throws an exception"));
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object)
        {
            GetAccessKeyRetryInterval = TimeSpan.Zero
        };
        var isAuthorizedField = typeof(MicrosoftEntraAccessKey).GetField("_isAuthorized", BindingFlags.NonPublic | BindingFlags.Instance);
        isAuthorizedField.SetValue(key, isAuthorized);
        Assert.Equal(isAuthorized, (bool)isAuthorizedField.GetValue(key));

        var updateAt = DateTime.UtcNow - TimeSpan.FromMinutes(timeElapsed);
        var updateAtField = typeof(MicrosoftEntraAccessKey).GetField("_updateAt", BindingFlags.NonPublic | BindingFlags.Instance);
        updateAtField.SetValue(key, updateAt);

        var initializedTcsField = typeof(MicrosoftEntraAccessKey).GetField("_initializedTcs", BindingFlags.NonPublic | BindingFlags.Instance);
        var initializedTcs = (TaskCompletionSource<object>)initializedTcsField.GetValue(key);

        await key.UpdateAccessKeyAsync().OrTimeout(TimeSpan.FromSeconds(30));
        var actualUpdateAt = Assert.IsType<DateTime>(updateAtField.GetValue(key));

        Assert.Equal(skip && isAuthorized, Assert.IsType<bool>(isAuthorizedField.GetValue(key)));

        if (skip)
        {
            Assert.Equal(updateAt, actualUpdateAt);
            Assert.False(initializedTcs.Task.IsCompleted);
        }
        else
        {
            Assert.True(updateAt < actualUpdateAt);
            Assert.True(initializedTcs.Task.IsCompleted);
        }

        if (hasException)
        {
            Assert.NotNull(key.LastException);
        }
        else
        {
            Assert.Null(key.LastException);
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
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object)
        {
            GetAccessKeyRetryInterval = TimeSpan.Zero
        };

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
    public async Task TestNotInitialized()
    {
        var mockCredential = new Mock<TokenCredential>();
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object);

        var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await key.GenerateAccessTokenAsync("", [], TimeSpan.FromSeconds(1), AccessTokenAlgorithm.HS256, source.Token)
        );
        Assert.Contains("initialization timed out", exception.Message);
    }

    [Theory]
    [ClassData(typeof(NotAuthorizedTestData))]
    public async Task TestUpdateAccessKeyFailedThrowsNotAuthorizedException(AzureSignalRException e, string expectedErrorMessage)
    {
        var mockCredential = new Mock<TokenCredential>();
        mockCredential.Setup(credential => credential.GetTokenAsync(
            It.IsAny<TokenRequestContext>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(e);
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object)
        {
            GetAccessKeyRetryInterval = TimeSpan.Zero,
        };

        var audience = "http://localhost/chat";
        var claims = Array.Empty<Claim>();
        var lifetime = TimeSpan.FromHours(1);
        var algorithm = AccessTokenAlgorithm.HS256;

        await key.UpdateAccessKeyAsync();

        var exception = await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(
            async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm)
        );
        Assert.Same(exception.InnerException, e);
        Assert.Same(exception.InnerException, key.LastException);
        Assert.StartsWith($"TokenCredentialProxy is not available for signing client tokens", exception.Message);
        Assert.Contains(expectedErrorMessage, exception.Message);

        var (kid, accessKey) = ("foo", DefaultSigningKey);
        key.UpdateAccessKey(kid, accessKey);
        Assert.Null(key.LastException);
    }

    [Theory]
    [InlineData(DefaultSigningKey)]
    [InlineData("fooooooooooooooooooooooooooooooooobar")]
    public async Task TestUpdateAccessKeySendRequest(string expectedKeyStr)
    {
        var expectedKid = "foo";
        var text = "{" + string.Format("\"AccessKey\": \"{0}\", \"KeyId\": \"{1}\"", expectedKeyStr, expectedKid) + "}";
        var httpClientFactory = new TestHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = TextHttpContent.From(text),
        });

        var credential = new TestTokenCredential(TokenType.MicrosoftEntra);
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, credential, httpClientFactory: httpClientFactory);

        await key.UpdateAccessKeyAsync();

        Assert.True(key.Available);
        Assert.Equal(expectedKid, key.Kid);
        Assert.Equal(expectedKeyStr, Encoding.UTF8.GetString(key.KeyBytes));
    }

    [Fact]
    public async Task TestLazyLoadAccessKey()
    {
        var expectedKeyStr = DefaultSigningKey;
        var expectedKid = "foo";
        var text = "{" + string.Format("\"AccessKey\": \"{0}\", \"KeyId\": \"{1}\"", expectedKeyStr, expectedKid) + "}";
        var httpClientFactory = new TestHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = TextHttpContent.From(text),
        });

        var credential = new TestTokenCredential(TokenType.MicrosoftEntra);
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, credential, httpClientFactory: httpClientFactory);

        Assert.False(key.Initialized);

        var token = await key.GenerateAccessTokenAsync("https://localhost", [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        Assert.NotNull(token);

        Assert.True(key.Initialized);
    }

    [Fact]
    public async Task TestLazyLoadAccessKeyFailed()
    {
        var mockCredential = new Mock<TokenCredential>();
        mockCredential.Setup(credential => credential.GetTokenAsync(
            It.IsAny<TokenRequestContext>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, mockCredential.Object)
        {
            GetAccessKeyRetryInterval = TimeSpan.FromSeconds(1),
        };

        Assert.False(key.Initialized);

        var task1 = key.GenerateAccessTokenAsync("https://localhost", [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        var task2 = key.UpdateAccessKeyAsync();
        Assert.True(task2.IsCompleted); // another task is in progress.

        await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task1);

        Assert.True(key.Initialized);
    }

    [Theory]
    [InlineData(TokenType.Local)]
    [InlineData(TokenType.MicrosoftEntra)]
    public async Task ThrowUnauthorizedExceptionTest(TokenType tokenType)
    {
        var endpoint = new Uri("https://test-aad-signalr.service.signalr.net");

        var message = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint),
            Content = TextHttpContent.From("")
        };
        var key = new MicrosoftEntraAccessKey(
            endpoint,
            new TestTokenCredential(tokenType),
            httpClientFactory: new TestHttpClientFactory(message)
        )
        {
            GetAccessKeyRetryInterval = TimeSpan.Zero
        };

        await key.UpdateAccessKeyAsync();

        Assert.False(key.Available);
        var ex = Assert.IsType<AzureSignalRUnauthorizedException>(key.LastException);

        var expected = tokenType switch
        {
            TokenType.Local => AzureSignalRUnauthorizedException.ErrorMessageLocalAuth,
            TokenType.MicrosoftEntra => AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra,
            _ => throw new NotImplementedException()
        };
        Assert.StartsWith("401 Unauthorized,", ex.Message);
        Assert.Contains(expected, ex.Message);
        Assert.EndsWith($"Request Uri: {endpoint}api/v1/auth/accessKey", ex.Message);
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
            new TestTokenCredential(TokenType.MicrosoftEntra),
            httpClientFactory: new TestHttpClientFactory(message)
        )
        {
            GetAccessKeyRetryInterval = TimeSpan.Zero
        };

        await key.UpdateAccessKeyAsync();

        Assert.False(key.Available);
        var ex = Assert.IsType<AzureSignalRRuntimeException>(key.LastException);
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);

        Assert.StartsWith("403 Forbidden,", ex.Message);
        Assert.Contains(expected, ex.Message);
        Assert.EndsWith($"Request Uri: {endpoint}api/v1/auth/accessKey", ex.Message);
    }

    public class NotAuthorizedTestData : IEnumerable<object[]>
    {
        private const string DefaultUri = "https://microsoft.com";

        public IEnumerator<object[]> GetEnumerator()
        {
            var accessKey = new AccessKey(DefaultSigningKey);
            var token1 = AuthUtility.GenerateJwtToken(accessKey.KeyBytes, issuer: Constants.AsrsTokenIssuer);
            var token2 = AuthUtility.GenerateJwtToken(accessKey.KeyBytes, issuer: "microsoft.com");

            yield return [new AzureSignalRUnauthorizedException(null, new Exception(), token1), AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra];
            yield return [new AzureSignalRUnauthorizedException(null, new Exception(), token2), AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra];
            yield return [new AzureSignalRUnauthorizedException("https://request.uri", new Exception(), token2), AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra];
            yield return [new AzureSignalRRuntimeException(DefaultUri, new Exception(), HttpStatusCode.Forbidden, "nginx"), AzureSignalRRuntimeException.NetworkErrorMessage];
            yield return [new AzureSignalRRuntimeException(DefaultUri, new Exception(), HttpStatusCode.Forbidden, "http-content"), "http-content"];
            yield return [new AzureSignalRRuntimeException(DefaultUri, new Exception("inner-exception-message"), HttpStatusCode.NotFound, "http"), AzureSignalRRuntimeException.ErrorMessage];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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
            Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
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

    public enum TokenType
    {
        Local,
        MicrosoftEntra,
    }

    private sealed class TestTokenCredential(TokenType tokenType) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var issuer = tokenType switch
            {
                TokenType.Local => Constants.AsrsTokenIssuer,
                TokenType.MicrosoftEntra => "microsoft.com",
                _ => throw new NotImplementedException(),
            };
            var key = new AccessKey(DefaultSigningKey);
            var token = AuthUtility.GenerateJwtToken(key.KeyBytes, issuer: issuer);
            return new AccessToken(token, DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(1)));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(GetToken(requestContext, cancellationToken));
        }
    }
}
