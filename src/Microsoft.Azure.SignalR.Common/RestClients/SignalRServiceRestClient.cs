// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR
{
    public partial class SignalRServiceRestClient
    {
        private readonly string _userAgent;

        public SignalRServiceRestClient(string userAgent, ServiceClientCredentials credentials, HttpClient httpClient, bool disposeHttpClient) : this(credentials, httpClient, disposeHttpClient)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                throw new ArgumentException($"'{nameof(userAgent)}' cannot be null or whitespace", nameof(userAgent));
            }

            _userAgent = userAgent;
        }

        partial void CustomInitialize()
        {
            HttpClient.DefaultRequestHeaders.Add(Constants.AsrsUserAgent, _userAgent);
        }
    }
}
