
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
        private DelegatingHandler[] _handlers;
        private HttpClientHandler _rootHandler;

        public RestClientBuilder(ServiceEndpoint endpoint)
        {
            _baseUri = new Uri(endpoint.Endpoint);
            _credentials = new JwtTokenCredentials(endpoint.AccessKey);
        }

        public RestClientBuilder(string connectionString) : this(new ServiceEndpoint(connectionString)) { }

        public RestClientBuilder WithProductInfo(string usegAgent)
        {
            var azUserAgentHandler = new AsrsUserAgentHandler(usegAgent);
            _handlers = new DelegatingHandler[] { azUserAgentHandler };
            return this;
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