// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class TestRestClientFactory : RestClientFactory
    {
        public TestRestClientFactory(string userAgent, HttpStatusCode code) : base(userAgent)
        {
            _httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(code));
        }

        public TestRestClientFactory(string userAgent, HttpStatusCode code, string content) : base(userAgent)
        {
            _httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(code, content));
        }

        public TestRestClientFactory(string userAgent, Action<HttpRequestMessage, CancellationToken> callback) : base(userAgent)
        {
            _httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(callback));
        }
    }
}