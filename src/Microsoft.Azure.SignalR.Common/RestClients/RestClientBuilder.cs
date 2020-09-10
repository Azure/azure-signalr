
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR
{
    internal class RestClientBuilder
    {
        private readonly Uri _baseUri;
        private readonly ServiceClientCredentials _credentials;
        private readonly List<DelegatingHandler> _handlers = new List<DelegatingHandler>();
        private AsrsUserAgentHandler _azUserAgentHandler;
        private HttpClientHandler _rootHandler;

        public RestClientBuilder(ServiceEndpoint endpoint)
        {
            _baseUri = new Uri(endpoint.Endpoint);
            _credentials = new JwtTokenCredentials(endpoint.AccessKey);
        }

        public RestClientBuilder(string connectionString) : this(new ServiceEndpoint(connectionString)) { }

        public RestClientBuilder WithProductInfo(string usegAgent)
        {
            _azUserAgentHandler = new AsrsUserAgentHandler(usegAgent);
            return this;
        }

        internal RestClientBuilder WithRootHandler(HttpClientHandler rootHandler)
        {
            _rootHandler = rootHandler;
            return this;
        }

        internal SignalRServiceRestClient Build()
        {
            DelegatingHandler[] handlers = null;
            if (_azUserAgentHandler != null)
            {
                handlers = new DelegatingHandler[] { _azUserAgentHandler };
            }
            return new SignalRServiceRestClient(_baseUri, _credentials, _rootHandler, handlers);
        }
    }
}