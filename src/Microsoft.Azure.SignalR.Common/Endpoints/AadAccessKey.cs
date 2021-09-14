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

        private const string DefaultScope = "https://signalr.azure.com/.default";

        private static readonly TokenRequestContext _defaultRequestContext = new TokenRequestContext(new string[] { DefaultScope });
        private static readonly TimeSpan AuthorizeInterval = TimeSpan.FromMinutes(AuthorizeIntervalInMinute);
        private static readonly TimeSpan AuthorizeRetryInterval = TimeSpan.FromSeconds(AuthorizeRetryIntervalInSec);
        private static readonly TimeSpan AuthorizeTimeout = TimeSpan.FromSeconds(10);

        private readonly TaskCompletionSource<object> _initializedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        private volatile bool _isAuthorized = false;

        private DateTime _lastUpdatedTime = DateTime.MinValue;

        public bool Authorized => InitializedTask.IsCompleted && _isAuthorized;

        public TokenCredential TokenCredential { get; }

        internal string AuthorizeUrl { get; }

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
            var token = await TokenCredential.GetTokenAsync(_defaultRequestContext, ctoken);
            return token.Token;
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
            _lastUpdatedTime = DateTime.UtcNow;
            _isAuthorized = true;
            _initializedTcs.TrySetResult(null);
        }

        internal async Task UpdateAccessKeyAsync()
        {
            if (DateTime.UtcNow - _lastUpdatedTime < AuthorizeInterval)
            {
                return;
            }

            Exception latest = null;
            for (int i = 0; i < AuthorizeMaxRetryTimes; i++)
            {
                var source = new CancellationTokenSource(AuthorizeTimeout);
                try
                {
                    await AuthorizeAsync(source.Token);
                    return;
                }
                catch (Exception e)
                {
                    latest = e;
                    await Task.Delay(AuthorizeRetryInterval);
                }
            }

            _isAuthorized = false;
            _initializedTcs.TrySetResult(null);
            throw latest;
        }

        private async Task AuthorizeAsync(CancellationToken ctoken = default)
        {
            var aadToken = await GenerateAadTokenAsync(ctoken);
            await AuthorizeWithTokenAsync(aadToken, ctoken);
        }

        private async Task AuthorizeWithTokenAsync(string accessToken, CancellationToken token = default)
        {
            var api = new RestApiEndpoint(AuthorizeUrl, accessToken);

            await new RestClient().SendAsync(
                api,
                HttpMethod.Get,
                "",
                handleExpectedResponseAsync: HandleHttpResponseAsync,
                cancellationToken: token);
        }

        private async Task<bool> HandleHttpResponseAsync(HttpResponseMessage response)
        {
            if (await HandleHttpResponseAsyncCore(response))
            {
                return true;
            }
            return false;
        }

        private async Task<bool> HandleHttpResponseAsyncCore(HttpResponseMessage response)
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
