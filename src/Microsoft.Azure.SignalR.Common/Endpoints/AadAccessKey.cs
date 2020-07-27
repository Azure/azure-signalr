using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common.Auth;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class AadAccessKey : AccessKey
    {
        private readonly AuthOptions _authOptions;

        private readonly TaskCompletionSource<bool> _authorizeTcs = new TaskCompletionSource<bool>();

        public bool Authorized => AuthorizeTask.IsCompleted && AuthorizeTask.Result;

        public Task<bool> AuthorizeTask => _authorizeTcs.Task;

        public AadAccessKey(AuthOptions options) : base()
        {
            _authOptions = options;
        }

        public async Task AuthorizeAsync(string endpoint, int? port, string serverId)
        {
            var token = await GenerateAccessToken();
            await AuthorizeWithTokenAsync(endpoint, port, serverId, token);
        }

        public Task<string> GenerateAccessToken()
        {
            if (_authOptions is ITokenBasedAuthOptions options)
            {
                return options.AcquireAccessToken();
            }
            throw new InvalidOperationException("This accesskey is not able to generate AccessToken, a TokenBasedAuthOptions is required.");
        }

        private async Task AuthorizeWithTokenAsync(string endpoint, int? port, string serverId, string accessToken)
        {
            if (port != null && port != 443)
            {
                endpoint += $":{port}";
            }
            var api = new RestApiEndpoint(endpoint + "/api/v1/auth/accessKey", accessToken)
            {
                Query = new Dictionary<string, StringValues> { { "serverId", serverId } }
            };

            await new RestClient().SendAsync(api, HttpMethod.Get, "", handleExpectedResponseAsync: HandleHttpResponseAsync);
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

            _authorizeTcs.SetResult(true);
            return true;
        }
    }
}
