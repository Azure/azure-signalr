// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR
{
    internal class JwtTokenCredentials : ServiceClientCredentials
    {
        private RestApiAccessTokenGenerator RestApiAccessTokenGenerator { get; }

        public JwtTokenCredentials(string accessKey)
        {
            RestApiAccessTokenGenerator = new RestApiAccessTokenGenerator(new AccessKey(accessKey));
        }

        public override async Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var tokenString = await RestApiAccessTokenGenerator.Generate(request.RequestUri.ToString());
            HttpRequestHeaders headers = request.Headers;
            headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            await base.ProcessHttpRequestAsync(request, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
        }
    }
}
