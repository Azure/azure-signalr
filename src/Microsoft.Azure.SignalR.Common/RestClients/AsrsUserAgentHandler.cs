// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class AsrsUserAgentHandler
        : DelegatingHandler
    {
        private readonly string _asrsUserAgent;

        public AsrsUserAgentHandler(string userAgent)
        {
            _asrsUserAgent = userAgent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add(Constants.AsrsUserAgent, _asrsUserAgent);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
