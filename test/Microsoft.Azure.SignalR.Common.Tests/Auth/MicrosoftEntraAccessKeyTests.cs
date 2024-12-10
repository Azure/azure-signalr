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
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential());

        var (kid, accessKey) = ("foo", DefaultSigningKey);
        key.UpdateAccessKey(kid, accessKey);

        var token = await key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromHours(1), AccessTokenAlgorithm.HS256);
        Assert.NotNull(token);
        Assert.True(TokenUtilities.TryParseIssuer(token, out var issuer) && string.Equals(Constants.AsrsTokenIssuer, issuer));
    }

    [Fact]
    public async Task TestInitializeFailed()
    {
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential())
        {
            GetAccessKeyRetryInterval = TimeSpan.Zero
        };

        await key.UpdateAccessKeyAsync();

        var task = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromHours(1), AccessTokenAlgorithm.HS256);
        var exception = await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task TestNotInitailized()
    {
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential(delay: 10000));
        Assert.False(key.Available);

        var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task1 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromHours(1), AccessTokenAlgorithm.HS256, source.Token);

        Assert.Equal(task1, await Task.WhenAny(task1, Task.Delay(5000)));

        var exception = await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task1);
        Assert.Contains("is not available for signing client tokens", exception.Message);
        Assert.Contains("has not been initialized.", exception.Message);
    }

    [Fact]
    public async Task TestUnavailable()
    {
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential(delay: 10000));
        key.UpdateAccessKey("foo", "bar");
        Assert.True(key.Available);

        UpdateAtField?.SetValue(key, DateTime.UtcNow - TimeSpan.FromHours(3));
        Assert.False(key.Available);

        var source = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var task1 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromHours(1), AccessTokenAlgorithm.HS256, source.Token);

        Assert.Equal(task1, await Task.WhenAny(task1, Task.Delay(5000)));

        var exception = await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task1);
        Assert.Contains("is not available for signing client tokens", exception.Message);
        Assert.Contains("has expired.", exception.Message);
    }


    [Theory]
    [ClassData(typeof(NotAuthorizedTestData))]
    public async Task TestUpdateAccessKeyFailedThrowsNotAuthorizedException(AzureSignalRException e, string expectedErrorMessage)
    {
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential() { Exception = e })
        {
            GetAccessKeyRetryInterval = TimeSpan.Zero,
        };

        await key.UpdateAccessKeyAsync();

        var task = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromHours(1), AccessTokenAlgorithm.HS256);
        var exception = await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task);
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

        var token = await key.GenerateAccessTokenAsync("https://localhost", [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        Assert.NotNull(token);
    }

    [Fact]
    public async Task TestLazyLoadAccessKeyFailed()
    {
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, new TestTokenCredential())
        {
            GetAccessKeyRetryInterval = TimeSpan.FromSeconds(1),
        };

        var task1 = key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        var task2 = key.UpdateAccessKeyAsync();
        Assert.False(task2.IsCompleted);

        await Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(async () => await task1);
        await task2;
        Assert.False(key.Available);
        Assert.False(key.NeedRefresh);
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
        Assert.False(key.Available);
        Assert.True(key.NeedRefresh);

        var token = await key.GenerateAccessTokenAsync(DefaultAudience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);
        Assert.True(TokenUtilities.TryParseIssuer(token, out var issuer));
        Assert.Equal(Constants.AsrsTokenIssuer, issuer);

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

        var credential = new TestTokenCredential(TokenType.MicrosoftEntra) { Exception = new InvalidOperationException() };
        var key = new MicrosoftEntraAccessKey(DefaultEndpoint, credential, httpClientFactory: httpClientFactory)
        {
            GetAccessKeyRetryInterval = TimeSpan.FromSeconds(1)
        };
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
        credential.Exception = null;
        UpdateAtField?.SetValue(key, DateTime.UtcNow - TimeSpan.FromMinutes(6));
        Assert.False(key.Available);
        Assert.True(key.NeedRefresh);

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
            Assert.Equal("Bearer", request.Headers?.Authorization?.Scheme);
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

    private sealed class TestTokenCredential(TokenType? tokenType = null, int delay = 0) : TokenCredential
    {
        public Exception? Exception { get; set; }

        private volatile int _count;

        public Exception Error { get; set; } = new InvalidOperationException();

        public int Count => _count;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);

            if (Exception != null)
            {
                throw Exception;
            }

            var issuer = tokenType switch
            {
                TokenType.Local => Constants.AsrsTokenIssuer,
                TokenType.MicrosoftEntra => "microsoft.com",
                _ => throw new InvalidOperationException(),
            };
            var token = AuthUtility.GenerateJwtToken(Encoding.UTF8.GetBytes(DefaultSigningKey), issuer: issuer);
            return new AccessToken(token, DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(1)));
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return GetToken(requestContext, cancellationToken);
        }
    }
}
