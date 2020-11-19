using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class AadAccessKey : AccessKey, IDisposable
    {
        private const int AuthorizeIntervalInMinute = 55;
        private const int AuthorizeMaxRetryTimes = 3;
        private const int AuthorizeRetryIntervalInSec = 3;

        private static readonly TimeSpan AuthorizeInterval = TimeSpan.FromMinutes(AuthorizeIntervalInMinute);
        private static readonly TimeSpan AuthorizeRetryInterval = TimeSpan.FromSeconds(AuthorizeRetryIntervalInSec);
        private static readonly TimeSpan AuthorizeTimeout = TimeSpan.FromSeconds(10);

        private int initialized = 0;

        private readonly TaskCompletionSource<bool> _authorizeTcs = new TaskCompletionSource<bool>();

        private readonly TimerAwaitable _timer = new TimerAwaitable(TimeSpan.Zero, AuthorizeInterval);

        public bool Authorized => AuthorizeTask.IsCompleted && AuthorizeTask.Result;

        public AuthOptions Options { get; }

        private Task<bool> AuthorizeTask => _authorizeTcs.Task;

        public AadAccessKey(AuthOptions options, string endpoint, int? port) : base(endpoint, port)
        {
            Options = options;
        }

        internal async Task AuthorizeAsync(string serverId, CancellationToken token = default)
        {
            var aadToken = await GenerateAadToken();
            await AuthorizeWithTokenAsync(Endpoint, Port, serverId, aadToken, token);
        }

        public void Dispose()
        {
            ((IDisposable)_timer).Dispose();
        }

        public Task<string> GenerateAadToken()
        {
            if (Options is IAadTokenGenerator options)
            {
                return options.AcquireAccessToken();
            }
            throw new InvalidOperationException("This accesskey is not able to generate AccessToken, a TokenBasedAuthOptions is required.");
        }

        public override async Task<string> GenerateAccessToken(
            string audience,
            IEnumerable<Claim> claims,
            TimeSpan lifetime,
            AccessTokenAlgorithm algorithm)
        {
            await AuthorizeTask;
            return await base.GenerateAccessToken(audience, claims, lifetime, algorithm);
        }

        public async Task UpdateAccessKeyAsync(IServerNameProvider provider, ILoggerFactory loggerFactory)
        {
            if (Interlocked.CompareExchange(ref initialized, 1, 0) == 1)
            {
                return;
            }

            _timer.Start();

            var logger = loggerFactory.CreateLogger<AadAccessKey>();

            while (await _timer)
            {
                var isAuthorized = false;
                for (int i = 0; i < AuthorizeMaxRetryTimes; i++)
                {
                    var source = new CancellationTokenSource(AuthorizeTimeout);
                    try
                    {
                        await AuthorizeAsync(provider.GetName(), source.Token);
                        Log.SucceedAuthorizeAccessKey(logger, Endpoint);
                        isAuthorized = true;
                        break;
                    }
                    catch (Exception e)
                    {
                        Log.FailedAuthorizeAccessKey(logger, Endpoint, e);
                        await Task.Delay(AuthorizeRetryInterval);
                    }
                }

                if (!isAuthorized)
                {
                    Log.ErrorAuthorizeAccessKey(logger, Endpoint);
                }
            }
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
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);

            if (!obj.TryGetValue("KeyId", out var keyId) || keyId.Type != JTokenType.String)
            {
                throw new ArgumentNullException("Missing required <KeyId> field.");
            }
            if (!obj.TryGetValue("AccessKey", out var key) || key.Type != JTokenType.String)
            {
                throw new ArgumentNullException("Missing required <AccessKey> field.");
            }
            Key = new Tuple<string, string>(keyId.ToString(), key.ToString());

            _authorizeTcs.TrySetResult(true);
            return true;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _errorAuthorize =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "ErrorAuthorizeAccessKey"), "Failed in authorizing AccessKey for '{endpoint}' after retried " + AuthorizeMaxRetryTimes + " times.");

            private static readonly Action<ILogger, string, Exception> _failedAuthorize =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "FailedAuthorizeAccessKey"), "Failed in authorizing AccessKey for '{endpoint}', will retry in " + AuthorizeRetryIntervalInSec + " seconds");

            private static readonly Action<ILogger, string, Exception> _succeedAuthorize =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "SucceedAuthorizeAccessKey"), "Succeed in authorizing AccessKey for '{endpoint}'");

            public static void ErrorAuthorizeAccessKey(ILogger logger, string endpoint)
            {
                _errorAuthorize(logger, endpoint, null);
            }

            public static void FailedAuthorizeAccessKey(ILogger logger, string endpoint, Exception e)
            {
                _failedAuthorize(logger, endpoint, e);
            }

            public static void SucceedAuthorizeAccessKey(ILogger logger, string endpoint)
            {
                _succeedAuthorize(logger, endpoint, null);
            }
        }
    }
}
