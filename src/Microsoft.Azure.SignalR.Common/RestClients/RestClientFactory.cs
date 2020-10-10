// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    internal class RestClientFactory
    {
        private const string HttpClientName = nameof(SignalRServiceRestClient);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _userAgent;

        internal RestClientFactory(string userAgent, Action<IHttpClientBuilder> action = null)
        {
            var httpClientBuilder = new ServiceCollection()
                .AddHttpClient(HttpClientName);
            action?.Invoke(httpClientBuilder); //hook for test
            _httpClientFactory = httpClientBuilder.Services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            _userAgent = userAgent;
        }

        internal SignalRServiceRestClient Create(ServiceEndpoint endpoint)
        {
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            var credentials = new JwtTokenCredentials(endpoint.AccessKey);
            var restClient = new SignalRServiceRestClient(_userAgent, credentials, httpClient, false)
            {
                BaseUri = new Uri(endpoint.Endpoint)
            };
            return restClient;
        }
    }
}