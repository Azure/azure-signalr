
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    public class RestClientFactory
    {
        private const string HttpClientName = "HttpClientWithUserAgent";
        private IHttpClientFactory _httpClientFactory;
        private protected readonly IHttpClientBuilder _httpClientBuilder;//for test, enable hook operations on the builder object before generating the _httpClientFactory.

        internal RestClientFactory(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                throw new ArgumentException($"'{nameof(userAgent)}' cannot be null or whitespace", nameof(userAgent));
            }
            _httpClientBuilder = new ServiceCollection()
                .AddHttpClient(HttpClientName)
                .ConfigureHttpClient(httpClient => httpClient.DefaultRequestHeaders.Add(Constants.AsrsUserAgent, userAgent));
        }

        internal SignalRServiceRestClient Create(ServiceEndpoint endpoint)
        {
            if (_httpClientFactory == null)
            {
                _httpClientFactory = _httpClientBuilder.Services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            }
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            var credentials = new JwtTokenCredentials(endpoint.AccessKey);
            var restClient = new SignalRServiceRestClient(credentials, httpClient, false)
            {
                BaseUri = new Uri(endpoint.Endpoint)
            };
            return restClient;
        }
    }
}