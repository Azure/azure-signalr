using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Identity.Client;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        private readonly TaskCompletionSource<bool> _initializedTcs = new TaskCompletionSource<bool>();

        private volatile string _accessKey;

        private volatile string _kid;

        public string Id => _kid;

        public bool Initialized => InitializedTask.IsCompleted && InitializedTask.Result;

        public Task<bool> InitializedTask => _initializedTcs.Task;

        public string Value => _accessKey;

        public AccessKey(string key = null)
        {
            if (!string.IsNullOrEmpty(key))
            {
                _accessKey = key;
                _kid = key.GetHashCode().ToString();
                _initializedTcs.SetResult(true);
            }
        }

        public static async Task AuthorizeTask(IConfidentialClientApplication app, AccessKey key, string endpoint, int? port)
        {
            var scopes = new string[] { ".default" };
            var token = await app.AcquireTokenForClient(scopes).WithSendX5C(true).ExecuteAsync();
            await key.Authorize(endpoint, port, token.AccessToken);
        }

        private async Task Authorize(string endpoint, int? port, string accessToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("serverId", "localhost");

            var builder = new UriBuilder(endpoint + "/auth/accessKey")
            {
                Port = port ?? 443,
                Query = query.ToString()
            };
            HttpResponseMessage response = await client.GetAsync(builder.ToString());

            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);

            if (obj.TryGetValue("AccessKey", out var key) && key.Type == JTokenType.String)
            {
                _accessKey = key.ToString();
            }
            if (obj.TryGetValue("KeyId", out var keyId) && keyId.Type == JTokenType.String)
            {
                _kid = keyId.ToString();
            }
            _initializedTcs.SetResult(true);
        }
    }
}
