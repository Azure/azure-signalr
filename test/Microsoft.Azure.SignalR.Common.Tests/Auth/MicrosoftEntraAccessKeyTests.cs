using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

#nullable enable

[Collection("Auth")]
public class MicrosoftEntraAccessKeyTests
{
    private const string DefaultSigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const string DefaultToken = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const string DefaultAudience = "https://localhost";

    private static readonly Uri DefaultEndpoint = new("http://localhost");

    private static readonly FieldInfo? UpdateAtField = typeof(MicrosoftEntraAccessKey).GetField("_updateAt", BindingFlags.Instance | BindingFlags.NonPublic);

    public enum TokenType
    {
        Local,

        MicrosoftEntra,

        Throws,
    }

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
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential(TokenType.Throws));

        var audience = "http://localhost/chat";
        var claims = Array.Empty<Claim>();
        var lifetime = TimeSpan.FromHours(1);
        var algorithm = AccessTokenAlgorithm.HS256;

        var (kid, accessKey) = ("foo", DefaultSigningKey);
        key.UpdateAccessKey(kid, accessKey);

        var token = await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm);
        Assert.NotNull(token);
    }

    [Fact]
    public async Task TestInitializeFailed()
    {
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential(TokenType.Throws))
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
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential(TokenType.Throws));

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
        var credential = new TestTokenCredential(TokenType.Throws)
        {
            Error = e,
        };
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, credential)
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
        Assert.StartsWith($"{nameof(TestTokenCredential)} is not available for signing client tokens", exception.Message);
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
        var credential = new TestTokenCredential(TokenType.MicrosoftEntra);
        var text = JsonSerializer.Serialize(new AccessKeyResponse()
        {
            AccessKey = DefaultSigningKey,
            KeyId = "foo"
        });
        var httpClientFactory = new TestHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = TextHttpContent.From(text),
        });
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, credential, httpClientFactory: httpClientFactory);

        Assert.False(key.Initialized);

        var token = await key.GenerateAccessTokenAsync("https://localhost", [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        Assert.NotNull(token);

        Assert.True(key.Initialized);
    }

    [Fact]
    public async Task TestLazyLoadAccessKeyFailed()
    {
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential(TokenType.Throws))
        {
            GetAccessKeyRetryInterval = TimeSpan.FromSeconds(1),
        };

        Assert.False(key.Initialized);

        var task1 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        var task2 = key.UpdateAccessKeyAsync();
        Assert.True(task2.IsCompleted); // another task is in progress.

        await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task1);

        Assert.True(key.Initialized);
    }

    [Fact]
    public async Task TestRefreshAccessKey()
    {
        var text = JsonSerializer.Serialize(new AccessKeyResponse()
        {
            AccessKey = DefaultSigningKey,
            KeyId = "foo"
        });
        var httpClientFactory = new TestHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = TextHttpContent.From(text),
        });

        var credential = new TestTokenCredential(TokenType.MicrosoftEntra);
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, credential, httpClientFactory: httpClientFactory);
        Assert.False(key.Initialized);
        Assert.False(key.Available);
        Assert.True(key.NeedRefresh);

        var token = await key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        Assert.True(TokenUtilities.TryParseIssuer(token, out var issuer));
        Assert.Equal(Constants.AsrsTokenIssuer, issuer);

        Assert.True(key.Initialized);
        Assert.True(key.Available);
        Assert.False(key.NeedRefresh);

        UpdateAtField?.SetValue(key, DateTime.UtcNow - TimeSpan.FromMinutes(56));
        Assert.True(key.Available);
        Assert.True(key.NeedRefresh);

        Assert.Equal(1, credential.Count);
        var task1 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        var task2 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        await Task.WhenAll(task1, task2);
        Assert.True(TokenUtilities.TryParseIssuer(await task1, out issuer));
        Assert.Equal(Constants.AsrsTokenIssuer, issuer);

        Assert.True(key.Available);
        Assert.False(key.NeedRefresh);
        Assert.Equal(2, credential.Count);
    }

    [Fact]
    public async Task TestRefreshAccessKeyUnauthorized()
    {
        var text = JsonSerializer.Serialize(new AccessKeyResponse()
        {
            AccessKey = DefaultSigningKey,
            KeyId = "foo"
        });
        var httpClientFactory = new TestHttpClientFactory(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = TextHttpContent.From(text),
        });

        var credential = new TestTokenCredential(TokenType.Throws);
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, credential, httpClientFactory: httpClientFactory)
        {
            GetAccessKeyRetryInterval = TimeSpan.FromSeconds(1)
        };
        Assert.False(key.Initialized);
        Assert.False(key.Available);
        Assert.True(key.NeedRefresh);

        await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256));

        Assert.False(key.Available);
        Assert.False(key.NeedRefresh);

        Assert.Equal(9, credential.Count); // GetMicrosoftEntraTokenRetry * GetAccessKeyRetry = 3 * 3
        await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256));
        Assert.Equal(9, credential.Count); // Does not trigger refresh

        // refresh, but still failed.
        UpdateAtField?.SetValue(key, DateTime.UtcNow - TimeSpan.FromMinutes(6));
        Assert.False(key.Available);
        Assert.True(key.NeedRefresh);
        
        var task1 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        var task2 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await Task.WhenAll(task1, task2));

        await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task1);
        await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task2);

        Assert.False(key.Available);
        Assert.False(key.NeedRefresh);
        Assert.Equal(18, credential.Count);

        // refresh, succeed.
        UpdateAtField?.SetValue(key, DateTime.UtcNow - TimeSpan.FromMinutes(6));
        Assert.False(key.Available);
        Assert.True(key.NeedRefresh);

        credential.TokenType = TokenType.MicrosoftEntra;
        task1 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        task2 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        await Task.WhenAll(task1, task2);

        Assert.True(TokenUtilities.TryParseIssuer(await task1, out var issuer));
        Assert.Equal(Constants.AsrsTokenIssuer, issuer);

        Assert.True(key.Available);
        Assert.False(key.NeedRefresh);
        Assert.Equal(19, credential.Count);
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
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            return Task.FromResult(message);
        }
    }

    private sealed class TextHttpContent : HttpContent
    {
        private readonly string _content;

        private TextHttpContent(string content)
        {
            _content = content;
        }

        internal static HttpContent From(string content) => new TextHttpContent(content);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            return stream.WriteAsync(Encoding.UTF8.GetBytes(_content)).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }
    }

    private sealed class TestTokenCredential(TokenType tokenType) : TokenCredential
    {
        private volatile int _count;

        public TokenType TokenType { get; set; } = tokenType;

        public Exception Error { get; set; } = new InvalidOperationException();

        public int Count => _count;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            var issuer = TokenType switch
            {
                TokenType.Local => Constants.AsrsTokenIssuer,
                TokenType.MicrosoftEntra => "microsoft.com",
                _ => throw Error,
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
