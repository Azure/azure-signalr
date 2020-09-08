
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
        private Uri BaseUri;
        private ServiceClientCredentials Credentials;
        private readonly List<DelegatingHandler> Handlers = new List<DelegatingHandler>();
        private AsrsUserAgentHandler AzUserAgentHandler;

        internal RestClientBuilder WithProductInfo(string productInfo)
        {
            AzUserAgentHandler = new AsrsUserAgentHandler(productInfo);
            return this;
        }

        internal RestClientBuilder WithServiceEndpoint(ServiceEndpoint serviceEndpoint)
        {
            BaseUri = new Uri(serviceEndpoint.Endpoint);
            Credentials = new JwtTokenCredentials(serviceEndpoint.AccessKey);
            return this;
        }

        internal RestClientBuilder WithHandler(IEnumerable<DelegatingHandler> handlers)
        {
            Handlers.AddRange(handlers);
            return this;
        }

        internal SignalRServiceRestClient Build()
        {
            List<DelegatingHandler> finalHandlers = new List<DelegatingHandler>();
            if (AzUserAgentHandler != null)
                finalHandlers.Add(AzUserAgentHandler);
            finalHandlers.AddRange(Handlers);
            return new SignalRServiceRestClient(BaseUri, Credentials, finalHandlers.ToArray());
        }
    }
}