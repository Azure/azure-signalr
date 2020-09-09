
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

        internal RestClientBuilder(ServiceEndpoint endpoint)
        {
            _baseUri = new Uri(endpoint.Endpoint);
            _credentials = new JwtTokenCredentials(endpoint.AccessKey);
        }

        internal RestClientBuilder(string connectionString) : this(new ServiceEndpoint(connectionString)) { }

        internal RestClientBuilder WithProductInfo(string usegAgent)
        {
            _azUserAgentHandler = new AsrsUserAgentHandler(usegAgent);
            return this;
        }

        internal RestClientBuilder WithHandler(DelegatingHandler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        internal SignalRServiceRestClient Build()
        {
            List<DelegatingHandler> finalHandlers = new List<DelegatingHandler>();
            if (_azUserAgentHandler != null)
            {
                finalHandlers.Add(_azUserAgentHandler);
            }
            finalHandlers.AddRange(_handlers);
            return new SignalRServiceRestClient(_baseUri, _credentials, finalHandlers.ToArray());
        }
    }
}