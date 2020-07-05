// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
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

        public async Task AuthorizeAsync(string endpoint, int? port, string serverId, AuthOptions options)
        {
            if (options is AzureActiveDirectoryOptions aadOptions)
            {
                var app = AzureActiveDirectoryHelper.BuildApplication(aadOptions);
                var token = await app.AcquireTokenForClient(AzureActiveDirectoryOptions.DefaultScopes).WithSendX5C(true).ExecuteAsync();
                await AuthorizeAsync(endpoint, port, serverId, token.AccessToken);
            }
        }

        internal async Task AuthorizeAsync(string endpoint, int? port, string serverId, string accessToken)
        {
            var client = HttpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var query = HttpUtility.ParseQueryString(string.Empty);
            query.Add("serverId", serverId);

            var builder = new UriBuilder(endpoint + "/api/v1/auth/accessKey")
            {
                Port = port ?? 443,
                Query = query.ToString()
            };
            var url = builder.ToString();
            HttpResponseMessage response = await client.GetAsync(url);

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
