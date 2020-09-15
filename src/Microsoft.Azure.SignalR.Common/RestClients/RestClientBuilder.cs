
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR
{
    internal class RestClientBuilder
    {
        private readonly Uri _baseUri;
        private readonly ServiceClientCredentials _credentials;
        private readonly DelegatingHandler[] _handlers;
        private HttpClientHandler _rootHandler;

        public RestClientBuilder(ServiceEndpoint endpoint, string userAgent) : this(userAgent)
        {
            _baseUri = new Uri(endpoint.Endpoint);
            _credentials = new JwtTokenCredentials(endpoint.AccessKey);
        }

        public RestClientBuilder(string connectionString, string userAgent) : this(new ServiceEndpoint(connectionString), userAgent) { }

        private RestClientBuilder(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                throw new ArgumentException($"'{nameof(userAgent)}' cannot be null or whitespace", nameof(userAgent));
            }

            var azUserAgentHandler = new AsrsUserAgentHandler(userAgent);
            _handlers = new DelegatingHandler[] { azUserAgentHandler };
        }

        internal RestClientBuilder WithRootHandler(HttpClientHandler rootHandler)
        {
            _rootHandler = rootHandler;
            return this;
        }

        internal SignalRServiceRestClient Build()
        {
            return new SignalRServiceRestClient(_baseUri, _credentials, _rootHandler, _handlers);
        }
    }
}