
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR
{
    public partial class GeneratedRestClient
    {
        private readonly string _productInfo;

        public GeneratedRestClient(Uri baseUri, ServiceClientCredentials credentials, string productInfo) : this(baseUri, credentials)
        {
            _productInfo = productInfo;
        }

        partial void CustomInitialize()
        {
            HttpClient.DefaultRequestHeaders.Add(Constants.AsrsUserAgent, _productInfo);
        }

        public static GeneratedRestClient Build(string connectionString, string productInfo)
        {
            var (endPoint, key, _, port) = ConnectionStringParser.Parse(connectionString);
            UriBuilder uriBuilder = new UriBuilder(endPoint);
            if (port.HasValue)
            {
                uriBuilder.Port = port.Value;
            }

            return new GeneratedRestClient(uriBuilder.Uri, new JwtTokenCredentials(key), productInfo);
        }
    }
}
