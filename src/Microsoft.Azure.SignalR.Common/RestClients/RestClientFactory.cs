// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    internal class RestClientFactory
    {
        private protected readonly IHttpClientFactory _httpClientFactory;
        private readonly string _userAgent;

        internal RestClientFactory(string userAgent)
        {
            var serviceCollection = new ServiceCollection()
                .AddHttpClient();
            _httpClientFactory = serviceCollection.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            _userAgent = userAgent;
        }

        private protected RestClientFactory(string userAgent, IHttpClientFactory httpClientFactory)
        {
            _userAgent = userAgent;
            _httpClientFactory = httpClientFactory;
        }

        protected virtual HttpClient CreateHttpClient() => _httpClientFactory.CreateClient();

        internal SignalRServiceRestClient Create(ServiceEndpoint endpoint)
        {
            var httpClient = CreateHttpClient();
            var credentials = new JwtTokenCredentials(endpoint.AccessKey);
            var restClient = new SignalRServiceRestClient(_userAgent, credentials, httpClient, true)
            {
                BaseUri = new Uri(endpoint.Endpoint)
            };
            return restClient;
        }
    }
}