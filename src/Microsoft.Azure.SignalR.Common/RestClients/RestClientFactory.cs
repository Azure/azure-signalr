// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    internal class RestClientFactory
    {
        private readonly Func<IHttpClientFactory, HttpClient> _genFunc = factory => factory.CreateClient();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _userAgent;

        internal RestClientFactory(string userAgent)
        {
            var serviceCollection = new ServiceCollection()
                .AddHttpClient();
            _httpClientFactory = serviceCollection.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            _userAgent = userAgent;
        }

        //hook for test
        private protected RestClientFactory(string userAgent, Func<IHttpClientFactory, HttpClient> genFunc, IHttpClientFactory httpClientFactory)
        {
            _userAgent = userAgent;
            _genFunc = genFunc;
            _httpClientFactory = httpClientFactory;
        }

        internal SignalRServiceRestClient Create(ServiceEndpoint endpoint)
        {
            var httpClient = _genFunc.Invoke(_httpClientFactory);
            var credentials = new JwtTokenCredentials(endpoint.AccessKey);
            var restClient = new SignalRServiceRestClient(_userAgent, credentials, httpClient, false)
            {
                BaseUri = new Uri(endpoint.Endpoint)
            };
            return restClient;
        }
    }
}