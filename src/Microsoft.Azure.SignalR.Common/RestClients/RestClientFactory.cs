// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;

namespace Microsoft.Azure.SignalR
{
    internal class RestClientFactory
    {
        protected readonly IHttpClientFactory _httpClientFactory;
        private readonly string _userAgent;
        private readonly string _serverName;

        public RestClientFactory(string userAgent, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _userAgent = userAgent;
            _serverName = RestApiAccessTokenGenerator.GenerateServerName();
        }

        internal SignalRServiceRestClient Create(ServiceEndpoint endpoint)
        {
            var httpClient = CreateHttpClient();
            var credentials = new JwtTokenCredentials(endpoint.AccessKey, _serverName);
            var restClient = new SignalRServiceRestClient(_userAgent, credentials, httpClient, true)
            {
                BaseUri = new Uri(endpoint.Endpoint)
            };
            return restClient;
        }

        protected virtual HttpClient CreateHttpClient() => _httpClientFactory.CreateClient();
    }
}