
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;

namespace Microsoft.Azure.SignalR
{
    internal class SignalRServiceRestClientHelper
    {
        internal SignalRServiceRestClient GetCustomizedClient(string connectionString, string productInfo, params DelegatingHandler[] handlers)
        {
            (string endPoint, string key, string _, int? port) = ConnectionStringParser.Parse(connectionString);
            UriBuilder uriBuilder = new UriBuilder(endPoint);
            if (port.HasValue)
            {
                uriBuilder.Port = port.Value;
            }

            var asrsUserAgentHandler = new AsrsUserAgentHandler(productInfo);
            DelegatingHandler[] finalHandlers;
            if (handlers != null)
            {
                finalHandlers = new DelegatingHandler[handlers.Length + 1];
                finalHandlers[0] = asrsUserAgentHandler;
                Array.Copy(handlers, 0, finalHandlers, 1, handlers.Length);
            }
            else
            {
                finalHandlers = new DelegatingHandler[] { asrsUserAgentHandler };
            }
            return new SignalRServiceRestClient(uriBuilder.Uri, new JwtTokenCredentials(key), finalHandlers);
        }
    }
}
