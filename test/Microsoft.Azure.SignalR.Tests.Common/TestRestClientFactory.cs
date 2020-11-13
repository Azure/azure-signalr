// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal class TestRestClientFactory : RestClientFactory
    {
        private const string HttpClientName = "TestRestClient";

        public TestRestClientFactory(string userAgent, HttpStatusCode code) : base(userAgent, CreateTestFactory(new TestRootHandler(code)))
        { }

        public TestRestClientFactory(string userAgent, HttpStatusCode code, string content) : base(userAgent, CreateTestFactory(new TestRootHandler(code, content)))
        { }

        public TestRestClientFactory(string userAgent, Action<HttpRequestMessage, CancellationToken> callback) : base(userAgent, CreateTestFactory(new TestRootHandler(callback)))
        { }

        protected override HttpClient CreateHttpClient()
        {
            return _httpClientFactory.CreateClient(HttpClientName);
        }

        private static IHttpClientFactory CreateTestFactory(TestRootHandler rootHandler)
        {
            var builder = new ServiceCollection().AddHttpClient(HttpClientName);
            builder.ConfigurePrimaryHttpMessageHandler(() => rootHandler);
            return builder.Services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        }
    }
}