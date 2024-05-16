// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;

using Microsoft.Azure.SignalR.Common;

using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKeyForMicrosoftEntra : AccessKey
    {
        internal const int GetAccessKeyIntervalInMinute = 55;

        internal const int GetAccessKeyMaxRetryTimes = 3;

        internal const int GetAccessKeyRetryIntervalInSec = 3;

        internal const int GetMicrosoftEntraTokenMaxRetryTimes = 3;

        internal static readonly TimeSpan GetAccessKeyTimeout = TimeSpan.FromSeconds(100);

        private const string DefaultScope = "https://signalr.azure.com/.default";

        private static readonly TokenRequestContext DefaultRequestContext = new TokenRequestContext(new string[] { DefaultScope });

        private static readonly TimeSpan GetAccessKeyInterval = TimeSpan.FromMinutes(GetAccessKeyIntervalInMinute);

        private static readonly TimeSpan GetAccessKeyIntervalWhenUnauthorized = TimeSpan.FromMinutes(5);

        private static readonly TimeSpan GetAccessKeyRetryInterval = TimeSpan.FromSeconds(GetAccessKeyRetryIntervalInSec);

        private readonly TaskCompletionSource<object> _initializedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        private volatile bool _isAuthorized = false;

        private DateTime _lastUpdatedTime = DateTime.MinValue;

        public bool Authorized
        {
            get => _isAuthorized;
            private set
            {
                _lastUpdatedTime = DateTime.UtcNow;
                _isAuthorized = value;
                _initializedTcs.TrySetResult(null);
            }
        }

        public TokenCredential TokenCredential { get; }

        internal string GetAccessKeyUrl { get; }

        internal bool HasExpired => DateTime.UtcNow - _lastUpdatedTime > TimeSpan.FromMinutes(GetAccessKeyIntervalInMinute * 2);

        private Task<object> InitializedTask => _initializedTcs.Task;

        public AccessKeyForMicrosoftEntra(Uri endpoint, TokenCredential credential, Uri serverEndpoint = null) : base(endpoint)
        {
            var authorizeUri = (serverEndpoint ?? endpoint).Append("/api/v1/auth/accessKey");
            GetAccessKeyUrl = authorizeUri.AbsoluteUri;
            TokenCredential = credential;
        }

        public virtual async Task<string> GetMicrosoftEntraTokenAsync(CancellationToken ctoken = default)
        {
            Exception latest = null;
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
            throw latest;
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
                if (Authorized)
                {
                    return await base.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm);
                }
                else
                {
                    throw new AzureSignalRAccessTokenNotAuthorizedException("The given AzureAD identity don't have the permission to generate access token.");
                }
            }
            else
            {
                throw new TaskCanceledException("Timeout reached when authorizing AzureAD identity.");
            }
        }

        internal void UpdateAccessKey(string kid, string accessKey)
        {
            Key = new Tuple<string, string>(kid, accessKey);
            Authorized = true;
        }

        internal async Task UpdateAccessKeyAsync(CancellationToken ctoken = default)
        {
            var delta = DateTime.UtcNow - _lastUpdatedTime;
            if (Authorized && delta < GetAccessKeyInterval)
            {
                return;
            }
            else if (!Authorized && delta < GetAccessKeyIntervalWhenUnauthorized)
            {
                return;
            }

            Exception latest = null;
            for (var i = 0; i < GetAccessKeyMaxRetryTimes; i++)
            {
                var source = new CancellationTokenSource(GetAccessKeyTimeout);
                var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(source.Token, ctoken);
                try
                {
                    var token = await GetMicrosoftEntraTokenAsync(linkedSource.Token);
                    await GetAccessKeyInternalAsync(token, linkedSource.Token);
                    return;
                }
                catch (OperationCanceledException e)
                {
                    latest = e;
                    break;
                }
                catch (Exception e)
                {
                    latest = e;
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

            Authorized = false;
            throw latest;
        }

        private async Task GetAccessKeyInternalAsync(string accessToken, CancellationToken ctoken = default)
        {
            var api = new RestApiEndpoint(GetAccessKeyUrl, accessToken);

            await new RestClient().SendAsync(
                api,
                HttpMethod.Get,
                handleExpectedResponseAsync: HandleHttpResponseAsync,
                cancellationToken: ctoken);
        }

        private async Task<bool> HandleHttpResponseAsync(HttpResponseMessage response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);

            if (!obj.TryGetValue("KeyId", out var keyId) || keyId.Type != JTokenType.String)
            {
                throw new AzureSignalRException("Missing required <KeyId> field.");
            }
            if (!obj.TryGetValue("AccessKey", out var key) || key.Type != JTokenType.String)
            {
                throw new AzureSignalRException("Missing required <AccessKey> field.");
            }

            UpdateAccessKey(keyId.ToString(), key.ToString());
            return true;
        }
    }
}
