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
    internal class AadAccessKey : AccessKey
    {
        internal const int AuthorizeIntervalInMinute = 55;

        internal const int AuthorizeMaxRetryTimes = 3;

        internal const int AuthorizeRetryIntervalInSec = 3;

        internal const int GetTokenMaxRetryTimes = 3;

        internal static readonly TimeSpan AuthorizeTimeout = TimeSpan.FromSeconds(10);

        private const string DefaultScope = "https://signalr.azure.com/.default";

        private static readonly TimeSpan AuthorizeInterval = TimeSpan.FromMinutes(AuthorizeIntervalInMinute);

        private static readonly TokenRequestContext DefaultRequestContext = new TokenRequestContext(new string[] { DefaultScope });

        private static readonly TimeSpan AuthorizeIntervalWhenFailed = TimeSpan.FromMinutes(5);

        private static readonly TimeSpan AuthorizeRetryInterval = TimeSpan.FromSeconds(AuthorizeRetryIntervalInSec);

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

        internal string AuthorizeUrl { get; }

        internal bool HasExpired => DateTime.UtcNow - _lastUpdatedTime > TimeSpan.FromMinutes(AuthorizeIntervalInMinute * 2);

        private Task<object> InitializedTask => _initializedTcs.Task;

        public AadAccessKey(Uri uri, TokenCredential credential) : base(uri)
        {
            var builder = new UriBuilder(Endpoint)
            {
                Path = "/api/v1/auth/accessKey",
                Port = uri.Port
            };
            AuthorizeUrl = builder.Uri.AbsoluteUri;
            TokenCredential = credential;
        }

        public virtual async Task<string> GenerateAadTokenAsync(CancellationToken ctoken = default)
        {
            Exception latest = null;
            for (var i = 0; i < GetTokenMaxRetryTimes; i++)
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
            if (Authorized && delta < AuthorizeInterval)
            {
                return;
            }
            else if (!Authorized && delta < AuthorizeIntervalWhenFailed)
            {
                return;
            }
            await AuthorizeWithRetryAsync(ctoken);
        }

        private async Task AuthorizeWithRetryAsync(CancellationToken ctoken = default)
        {
            Exception latest = null;
            for (var i = 0; i < AuthorizeMaxRetryTimes; i++)
            {
                var source = new CancellationTokenSource(AuthorizeTimeout);
                var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(source.Token, ctoken);
                try
                {
                    var token = await GenerateAadTokenAsync(linkedSource.Token);
                    await AuthorizeWithTokenAsync(token, linkedSource.Token);
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
                    await Task.Delay(AuthorizeRetryInterval);
                }
            }

            Authorized = false;
            throw latest;
        }

        private async Task AuthorizeWithTokenAsync(string accessToken, CancellationToken ctoken = default)
        {
            var api = new RestApiEndpoint(AuthorizeUrl, accessToken);

            await new RestClient().SendAsync(
                api,
                HttpMethod.Get,
                "",
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
