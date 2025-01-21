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

    private const int GetAccessKeyMaxRetryTimes = 3;

    private const int GetMicrosoftEntraTokenMaxRetryTimes = 3;

    private readonly object _lock = new object();

    private volatile TaskCompletionSource<bool> _updateTaskSource;

    private static readonly TokenRequestContext DefaultRequestContext = new TokenRequestContext(new string[] { Constants.AsrsDefaultScope });

    private static readonly TimeSpan GetAccessKeyInterval = TimeSpan.FromMinutes(55);

    private static readonly TimeSpan GetAccessKeyIntervalUnavailable = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan AccessKeyExpireTime = TimeSpan.FromMinutes(120);

    private readonly IHttpClientFactory _httpClientFactory;

    private volatile bool _isAuthorized = false;

    private DateTime _updateAt = DateTime.MinValue;

    private volatile string? _kid;

    private volatile byte[]? _keyBytes;

    public bool NeedRefresh => DateTime.UtcNow - _updateAt > (Available ? GetAccessKeyInterval : GetAccessKeyIntervalUnavailable);

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
        }
    }

    public TokenCredential TokenCredential { get; }

    public string Kid => _kid ?? throw new ArgumentNullException(nameof(Kid));

    public byte[] KeyBytes => _keyBytes ?? throw new ArgumentNullException(nameof(KeyBytes));

    internal Exception? LastException { get; private set; }

    internal string GetAccessKeyUrl { get; }

    internal TimeSpan GetAccessKeyRetryInterval { get; set; } = TimeSpan.FromSeconds(3);

    public MicrosoftEntraAccessKey(Uri serverEndpoint,
                                   TokenCredential credential,
                                   IHttpClientFactory? httpClientFactory = null)
    {
        var authorizeUri = serverEndpoint.Append("/api/v1/auth/accessKey");
        GetAccessKeyUrl = authorizeUri.AbsoluteUri;
        TokenCredential = credential;

        _httpClientFactory = httpClientFactory ?? HttpClientFactory.Instance;

        _updateTaskSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _updateTaskSource.TrySetResult(false);
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
        var updateTask = Task.CompletedTask;
        if (NeedRefresh)
        {
            updateTask = UpdateAccessKeyAsync();
        }

        if (!Available)
        {
            try
            {
                await updateTask.OrCancelAsync(ctoken);
            }
            catch (OperationCanceledException)
            {
            }
        }
        return Available
            ? AuthUtility.GenerateAccessToken(KeyBytes, Kid, audience, claims, lifetime, algorithm)
            : throw new AzureSignalRAccessTokenNotAuthorizedException(TokenCredential, GetExceptionMessage(LastException, _keyBytes != null), LastException);
    }

    internal void UpdateAccessKey(string kid, string keyStr)
    {
        _keyBytes = Encoding.UTF8.GetBytes(keyStr);
        _kid = kid;
        Available = true;

        lock (_lock)
        {
            _updateTaskSource.TrySetResult(true);
        }
    }

    internal async Task UpdateAccessKeyAsync()
    {
        TaskCompletionSource<bool> tcs;
        lock (_lock)
        {
            if (!_updateTaskSource.Task.IsCompleted)
            {
                tcs = _updateTaskSource;
            }
            else
            {
                _updateTaskSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = UpdateAccessKeyInternalAsync(_updateTaskSource);
                tcs = _updateTaskSource;
            }
        }
        await tcs.Task;
    }

    private async Task UpdateAccessKeyInternalAsync(TaskCompletionSource<bool> tcs)
    {
        for (var i = 0; i < GetAccessKeyMaxRetryTimes; i++)
        {
            using var source = new CancellationTokenSource(GetAccessKeyTimeout);
            try
            {
                await UpdateAccessKeyInternalAsync(source.Token);
                tcs.TrySetResult(true);
                return;
            }
            catch (OperationCanceledException e)
            {
                LastException = e; // retry immediately
            }
            catch (Exception e)
            {
                LastException = e;
                await Task.Delay(GetAccessKeyRetryInterval); // retry after interval.
            }
        }

        if (!Available)
        {
            // Update the status only when it becomes "not available" due to expiration to refresh updateAt.
            Available = false;
        }
        tcs.TrySetResult(false);
    }

    private static string GetExceptionMessage(Exception? exception, bool initialized)
    {
        return exception switch
        {
            AzureSignalRUnauthorizedException => AzureSignalRUnauthorizedException.ErrorMessageMicrosoftEntra,
            _ => exception?.Message ?? (initialized ? "The access key has expired." : "The access key has not been initialized."),
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
