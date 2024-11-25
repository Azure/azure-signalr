// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;

using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class MicrosoftEntraAccessKey : IAccessKey
{
    internal static readonly TimeSpan GetAccessKeyTimeout = TimeSpan.FromSeconds(100);

    private const int UpdateTaskIdle = 0;

    private const int UpdateTaskRunning = 1;

    private const int GetAccessKeyMaxRetryTimes = 3;

    private const int GetMicrosoftEntraTokenMaxRetryTimes = 3;

    private static readonly TokenRequestContext DefaultRequestContext = new TokenRequestContext(new string[] { Constants.AsrsDefaultScope });

    private static readonly TimeSpan GetAccessKeyInterval = TimeSpan.FromMinutes(55);

    private static readonly TimeSpan GetAccessKeyIntervalWhenUnauthorized = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan AccessKeyExpireTime = TimeSpan.FromMinutes(120);

    private readonly TaskCompletionSource<object?> _initializedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly IHttpClientFactory _httpClientFactory;

    private volatile int _updateState = 0;

    private volatile bool _isAuthorized = false;

    private DateTime _updateAt = DateTime.MinValue;

    private volatile string? _kid;

    private volatile byte[]? _keyBytes;

    public bool Initialized => _initializedTcs.Task.IsCompleted;

    public bool Available
    {
        get => _isAuthorized && DateTime.UtcNow - _updateAt < AccessKeyExpireTime;

        private set
        {
            if (value)
            {
                LastException = null;
            }
            _updateAt = DateTime.UtcNow;
            _isAuthorized = value;
            _initializedTcs.TrySetResult(null);
        }
    }

    public TokenCredential TokenCredential { get; }

    public string Kid => _kid ?? throw new ArgumentNullException(nameof(Kid));

    public byte[] KeyBytes => _keyBytes ?? throw new ArgumentNullException(nameof(KeyBytes));

    public Uri Endpoint { get; }

    internal Exception? LastException { get; private set; }

    internal string GetAccessKeyUrl { get; }

    internal TimeSpan GetAccessKeyRetryInterval { get; set; } = TimeSpan.FromSeconds(3);

    public MicrosoftEntraAccessKey(Uri endpoint,
                                   TokenCredential credential,
                                   Uri? serverEndpoint = null,
                                   IHttpClientFactory? httpClientFactory = null)
    {
        Endpoint = endpoint;

        var authorizeUri = (serverEndpoint ?? endpoint).Append("/api/v1/auth/accessKey");
        GetAccessKeyUrl = authorizeUri.AbsoluteUri;
        TokenCredential = credential;

        _httpClientFactory = httpClientFactory ?? HttpClientFactory.Instance;
    }

    public virtual async Task<string> GetMicrosoftEntraTokenAsync(CancellationToken ctoken = default)
    {
        Exception? latest = null;
        for (var i = 0; i < GetMicrosoftEntraTokenMaxRetryTimes; i++)
        {
            try
            {
                var token = await TokenCredential.GetTokenAsync(DefaultRequestContext, ctoken);
                return token.Token;
            }
            catch (Exception e)
            {
                latest = e;
            }
        }
        throw latest ?? new InvalidOperationException();
    }

    public async Task<string> GenerateAccessTokenAsync(string audience,
                                                       IEnumerable<Claim> claims,
                                                       TimeSpan lifetime,
                                                       AccessTokenAlgorithm algorithm,
                                                       CancellationToken ctoken = default)
    {
        if (!_initializedTcs.Task.IsCompleted)
        {
            var source = new CancellationTokenSource(Constants.Periods.DefaultUpdateAccessKeyTimeout);
            _ = UpdateAccessKeyAsync(source.Token);
        }

        await _initializedTcs.Task.OrCancelAsync(ctoken, "The access key initialization timed out.");

        return Available
            ? AuthUtility.GenerateAccessToken(KeyBytes, Kid, audience, claims, lifetime, algorithm)
            : throw new AzureSignalRAccessTokenNotAuthorizedException(TokenCredential, GetExceptionMessage(LastException), LastException);
    }

    internal void UpdateAccessKey(string kid, string keyStr)
    {
        _keyBytes = Encoding.UTF8.GetBytes(keyStr);
        _kid = kid;
        Available = true;
    }

    internal async Task UpdateAccessKeyAsync(CancellationToken ctoken = default)
    {
        var delta = DateTime.UtcNow - _updateAt;
        if (Available && delta < GetAccessKeyInterval)
        {
            return;
        }
        else if (!Available && delta < GetAccessKeyIntervalWhenUnauthorized)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _updateState, UpdateTaskRunning, UpdateTaskIdle) != UpdateTaskIdle)
        {
            return;
        }

        for (var i = 0; i < GetAccessKeyMaxRetryTimes; i++)
        {
            if (ctoken.IsCancellationRequested)
            {
                break;
            }

            var source = new CancellationTokenSource(GetAccessKeyTimeout);
            try
            {
                await UpdateAccessKeyInternalAsync(source.Token).OrCancelAsync(ctoken);
                Interlocked.Exchange(ref _updateState, UpdateTaskIdle);
                return;
            }
            catch (OperationCanceledException e)
            {
                LastException = e; // retry immediately
            }
            catch (Exception e)
            {
                LastException = e;
                try
                {
                    await Task.Delay(GetAccessKeyRetryInterval, ctoken); // retry after interval.
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (!Available)
        {
            // Update the status only when it becomes "not available" due to expiration to refresh updateAt.
            Available = false;
        }
        Interlocked.Exchange(ref _updateState, UpdateTaskIdle);
    }

    private static string GetExceptionMessage(Exception? exception)
    {
        return exception switch
        {
            AzureSignalRUnauthorizedException => AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra,
            _ => exception?.Message ?? "The access key has expired.",
        };
    }

    private static async Task ThrowExceptionOnResponseFailureAsync(HttpRequestMessage request, HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();

#if NET5_0_OR_GREATER
        var innerException = new HttpRequestException(
            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})",
            null,
            response.StatusCode);
#else
        var innerException = new HttpRequestException(
            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})");
#endif

        var requestUri = request.RequestUri?.ToString();
        var jwtToken = request.Headers.Authorization?.Parameter ?? null;
        throw response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new AzureSignalRInvalidArgumentException(requestUri, innerException, content),
            HttpStatusCode.Unauthorized => new AzureSignalRUnauthorizedException(requestUri, innerException, jwtToken),
            HttpStatusCode.NotFound => new AzureSignalRInaccessibleEndpointException(requestUri, innerException),
            _ => new AzureSignalRRuntimeException(requestUri, innerException, response.StatusCode, content),
        };
    }

    private async Task UpdateAccessKeyInternalAsync(CancellationToken ctoken)
    {
        var accessToken = await GetMicrosoftEntraTokenAsync(ctoken);

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(GetAccessKeyUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientNames.UserDefault);

        var response = await httpClient.SendAsync(request, ctoken);

        await HandleHttpResponseAsync(response);

        await ThrowExceptionOnResponseFailureAsync(request, response);
    }

    private async Task HandleHttpResponseAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        var obj = JsonSerializer.Deserialize<AccessKeyResponse>(content) ?? throw new AzureSignalRException("Access key response is not expected.");

        if (string.IsNullOrEmpty(obj.KeyId))
        {
            throw new AzureSignalRException("Missing required <KeyId> field.");
        }
        if (string.IsNullOrEmpty(obj.AccessKey))
        {
            throw new AzureSignalRException("Missing required <AccessKey> field.");
        }
        UpdateAccessKey(obj.KeyId, obj.AccessKey);
    }
}
