// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        private readonly TaskCompletionSource<bool> _initializedTcs = new TaskCompletionSource<bool>();

        private readonly AuthOptions _authOptions;

        private volatile string _accessKey;

        private volatile string _kid;

        public string Id => _kid;

        public bool Initialized => InitializedTask.IsCompleted && InitializedTask.Result;

        public Task<bool> InitializedTask => _initializedTcs.Task;

        public string Value => _accessKey;

        public AccessKey(string key)
        {
            _accessKey = key;
            _kid = key.GetHashCode().ToString();
            _initializedTcs.SetResult(true);
        }

        public AccessKey(AuthOptions options)
        {
            _authOptions = options;
        }

        public async Task AuthenticateAsync(string endpoint, int? port, string serverId)
        {
            if (_authOptions is IAadTokenGenerator options)
            {
                var token = await options.AcquireAccessToken();
                await AuthenticateWithTokenAsync(endpoint, port, serverId, token);
            }
            else
            {
                throw new InvalidOperationException($"{_authOptions.AuthType} is not valid.");
            }
        }

        internal async Task AuthenticateWithTokenAsync(string endpoint, int? port, string serverId, string accessToken)
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

            if (obj.TryGetValue("AccessKey", out var key) && key.Type == JTokenType.String)
            {
                _accessKey = key.ToString();
            }
            else
            {
                throw new ArgumentNullException("Missing required <AccessKey> field.");
            }

            if (obj.TryGetValue("KeyId", out var keyId) && keyId.Type == JTokenType.String)
            {
                _kid = keyId.ToString();
            }
            else
            {
                throw new ArgumentNullException("Missing required <KeyId> field.");
            }

            _initializedTcs.SetResult(true);

            return true;
        }
    }
}