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
        private readonly IHttpClientFactory _httpClientFactory;

        internal RestClientFactory(string userAgent, Action<IHttpClientBuilder> action = null)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                throw new ArgumentException($"'{nameof(userAgent)}' cannot be null or whitespace", nameof(userAgent));
            }
            var httpClientBuilder = new ServiceCollection()
                .AddHttpClient(HttpClientName)
                .ConfigureHttpClient(httpClient => httpClient.DefaultRequestHeaders.Add(Constants.AsrsUserAgent, userAgent));
            action?.Invoke(httpClientBuilder); //hook for test
            _httpClientFactory = httpClientBuilder.Services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        }

        internal SignalRServiceRestClient Create(ServiceEndpoint endpoint)
        {
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