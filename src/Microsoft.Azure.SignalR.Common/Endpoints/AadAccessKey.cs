using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class AadAccessKey : AccessKey
    {
        internal const int AuthorizeIntervalInMinute = 55;
        internal const int AuthorizeMaxRetryTimes = 3;
        internal const int AuthorizeRetryIntervalInSec = 3;

        private static readonly TimeSpan AuthorizeInterval = TimeSpan.FromMinutes(AuthorizeIntervalInMinute);
        private static readonly TimeSpan AuthorizeRetryInterval = TimeSpan.FromSeconds(AuthorizeRetryIntervalInSec);
        private static readonly TimeSpan AuthorizeTimeout = TimeSpan.FromSeconds(10);

        private readonly TaskCompletionSource<object> _initializedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        private volatile bool _isAuthorized = false;

        private DateTime _lastUpdatedTime = DateTime.MinValue;

        public bool Authorized => InitializedTask.IsCompleted && _isAuthorized;

        public AuthOptions Options { get; }

        private Task<object> InitializedTask => _initializedTcs.Task;

        public AadAccessKey(AuthOptions options, string endpoint, int? port) : base(endpoint, port)
        {
            Options = options;
        }

        public Task<string> GenerateAadTokenAsync(CancellationToken ctoken = default)
        {
            if (Options is IAadTokenGenerator options)
            {
                return options.AcquireAccessToken();
            }
            throw new InvalidOperationException("This accesskey is not able to generate AccessToken, a TokenBasedAuthOptions is required.");
        }

        public override async Task<string> GenerateAccessTokenAsync(
            string audience,
            IEnumerable<Claim> claims,
            TimeSpan lifetime,
            AccessTokenAlgorithm algorithm,
            CancellationToken ctoken = default)
        {
            await InitializedTask;
            if (!Authorized)
            {
                throw new AzureSignalRAccessTokenNotAuthorizedException();
            }
            return await base.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm);
        }

        private async Task AuthorizeAsync(string serverId, CancellationToken ctoken = default)
        {
            var aadToken = await GenerateAadTokenAsync(ctoken);
            await AuthorizeWithTokenAsync(Endpoint, Port, serverId, aadToken, ctoken);
        }

        private async Task AuthorizeWithTokenAsync(string endpoint, int? port, string serverId, string accessToken, CancellationToken token = default)
        {
            if (port != null && port != 443)
            {
                endpoint += $":{port}";
            }
            var api = new RestApiEndpoint(endpoint + "/api/v1/auth/accessKey", accessToken)
            {
                Query = new Dictionary<string, StringValues> { { "serverId", serverId } }
            };

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
            Key = new Tuple<string, string>(keyId.ToString(), key.ToString());

            return true;
        }

        internal async Task UpdateAccessKeyAsync(string serverName)
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
                    await AuthorizeAsync(serverName, source.Token);
                    _lastUpdatedTime = DateTime.UtcNow;
                    _isAuthorized = true;
                    _initializedTcs.TrySetResult(null);
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
    }
}
