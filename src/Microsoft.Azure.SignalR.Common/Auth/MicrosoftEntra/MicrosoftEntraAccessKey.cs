// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;

using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class MicrosoftEntraAccessKey : AccessKey
{
    internal static readonly TimeSpan GetAccessKeyTimeout = TimeSpan.FromSeconds(100);

    private const int GetAccessKeyIntervalInMinute = 55;

    private const int GetAccessKeyMaxRetryTimes = 3;

    private const int GetMicrosoftEntraTokenMaxRetryTimes = 3;

    private const string DefaultScope = "https://signalr.azure.com/.default";

    private static readonly TokenRequestContext DefaultRequestContext = new TokenRequestContext(new string[] { DefaultScope });

    private static readonly TimeSpan GetAccessKeyInterval = TimeSpan.FromMinutes(GetAccessKeyIntervalInMinute);

    private static readonly TimeSpan GetAccessKeyIntervalWhenUnauthorized = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan GetAccessKeyRetryInterval = TimeSpan.FromSeconds(3);

    private readonly TaskCompletionSource<object?> _initializedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly IHttpClientFactory _httpClientFactory;

    private volatile bool _isAuthorized = false;

    private DateTime _lastUpdatedTime = DateTime.MinValue;

    public bool IsAuthorized
    {
        get => _isAuthorized;

        private set
        {
            if (value)
            {
                LastException = null;
            }
            _lastUpdatedTime = DateTime.UtcNow;
            _isAuthorized = value;
            _initializedTcs.TrySetResult(null);
        }
    }

    public TokenCredential TokenCredential { get; }

    internal Exception? LastException { get; private set; }

    internal string GetAccessKeyUrl { get; }

    internal bool HasExpired => DateTime.UtcNow - _lastUpdatedTime > TimeSpan.FromMinutes(GetAccessKeyIntervalInMinute * 2);

    private Task<object?> InitializedTask => _initializedTcs.Task;

    public MicrosoftEntraAccessKey(Uri endpoint,
                                   TokenCredential credential,
                                   Uri? serverEndpoint = null,
                                   IHttpClientFactory? httpClientFactory = null) : base(endpoint)
    {
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

    public override async Task<string> GenerateAccessTokenAsync(
        string audience,
        IEnumerable<Claim> claims,
        TimeSpan lifetime,
        AccessTokenAlgorithm algorithm,
        CancellationToken ctoken = default)
    {
        var task = await Task.WhenAny(InitializedTask, ctoken.AsTask());

        if (task == InitializedTask || InitializedTask.IsCompleted)
        {
            await task;
            return IsAuthorized
                ? await base.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm, ctoken)
                : throw new AzureSignalRAccessTokenNotAuthorizedException(TokenCredential.GetType().Name, LastException);
        }
        else
        {
            throw new TaskCanceledException("Timeout reached when authorizing AzureAD identity.");
        }
    }

    internal void UpdateAccessKey(string kid, string accessKey)
    {
        Key = new Tuple<string, string>(kid, accessKey);
        IsAuthorized = true;
    }

    internal async Task UpdateAccessKeyAsync(CancellationToken ctoken = default)
    {
        var delta = DateTime.UtcNow - _lastUpdatedTime;
        if (IsAuthorized && delta < GetAccessKeyInterval)
        {
            return;
        }
        else if (!IsAuthorized && delta < GetAccessKeyIntervalWhenUnauthorized)
        {
            return;
        }

        for (var i = 0; i < GetAccessKeyMaxRetryTimes; i++)
        {
            var source = new CancellationTokenSource(GetAccessKeyTimeout);
            var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(source.Token, ctoken);
            try
            {
                await UpdateAccessKeyInternalAsync(linkedSource.Token);
                return;
            }
            catch (OperationCanceledException e)
            {
                LastException = e;
                break;
            }
            catch (Exception e)
            {
                LastException = e;
                try
                {
                    await Task.Delay(GetAccessKeyRetryInterval, ctoken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        IsAuthorized = false;
    }

    private static async Task ThrowExceptionOnResponseFailureAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync();

#if NET5_0_OR_GREATER
            var innerException = new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})",
                null,
                response.StatusCode);
#else
        var innerException = new HttpRequestException(
            $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase})");
#endif

        var requestUri = response.RequestMessage?.RequestUri?.ToString();
        throw response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new AzureSignalRInvalidArgumentException(requestUri, innerException, detail),
            HttpStatusCode.Unauthorized => new AzureSignalRUnauthorizedException(requestUri, innerException),
            HttpStatusCode.NotFound => new AzureSignalRInaccessibleEndpointException(requestUri, innerException),
            _ => new AzureSignalRRuntimeException(requestUri, innerException),
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

        await ThrowExceptionOnResponseFailureAsync(response);
    }

    private async Task<bool> HandleHttpResponseAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return false;
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
        return true;
    }
}
