using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class AadAccessKey : AccessKey
    {
        private readonly AuthOptions _authOptions;

        private readonly TaskCompletionSource<bool> _authorizeTcs = new TaskCompletionSource<bool>();

        public bool Authorized => AuthorizeTask.IsCompleted && AuthorizeTask.Result;

        private Task<bool> AuthorizeTask => _authorizeTcs.Task;

        public AadAccessKey(AuthOptions options) : base()
        {
            _authOptions = options;
        }

        public async Task AuthorizeAsync(string endpoint, int? port, string serverId, CancellationToken token = default)
        {
            var aadToken = await GenerateAadToken();
            await AuthorizeWithTokenAsync(endpoint, port, serverId, aadToken, token);
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

        public Task<string> GenerateAadToken()
        {
            if (_authOptions is IAadTokenGenerator options)
            {
                return options.GenerateAccessToken();
            }
            throw new InvalidOperationException("This accesskey is not able to generate AccessToken, a TokenBasedAuthOptions is required.");
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

            if (obj.TryGetValue("KeyId", out var keyId) && keyId.Type == JTokenType.String)
            {
                Id = keyId.ToString();
            }
            else
            {
                throw new ArgumentNullException("Missing required <KeyId> field.");
            }

            if (obj.TryGetValue("AccessKey", out var key) && key.Type == JTokenType.String)
            {
                Value = key.ToString();
            }
            else
            {
                throw new ArgumentNullException("Missing required <AccessKey> field.");
            }

            _authorizeTcs.TrySetResult(true);
            return true;
        }
    }
}
