// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class GroupManagerProxy : IGroupManager
    {
        private const int ProxyPort = 5002;

        private readonly string _baseUri;
        private readonly string _accessKey;

        public GroupManagerProxy(string endpoint, string accessKey, string hubName, HubProxyOptions options)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (string.IsNullOrEmpty(accessKey))
            {
                throw new ArgumentNullException(nameof(accessKey));
            }

            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var apiVersion = options?.ApiVersion ?? HubProxyOptions.DefaultApiVersion;
            _baseUri = $"{endpoint}:{ProxyPort}/{apiVersion}/hub/{hubName.ToLower()}/group";
            _accessKey = accessKey;
        }

        public Task AddAsync(string connectionId, string groupName)
        {
            var uri = GetRequestUri(connectionId, groupName);
            return SendAsync(uri, HttpMethod.Post);
        }

        public Task RemoveAsync(string connectionId, string groupName)
        {
            var uri = GetRequestUri(connectionId, groupName);
            return SendAsync(uri, HttpMethod.Delete);
        }

        private string GetRequestUri(string connectionId, string groupName)
        {
            return $"{_baseUri}/{groupName}/connection/{connectionId}";
        }

        private Task SendAsync(string uri, HttpMethod method)
        {
            var request = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri(uri)
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GenerateAccessToken(uri));

            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.AcceptCharset.Clear();
            request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("UTF-8"));

            return new HttpClient().SendAsync(request);
        }

        private string GenerateAccessToken(string audience)
        {
            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: null,
                expires: DateTime.UtcNow.Add(TokenProvider.DefaultAccessTokenLifetime),
                signingKey: _accessKey
            );
        }
    }
}
