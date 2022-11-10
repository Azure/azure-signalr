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
        private readonly RestApiAccessTokenGenerator _restApiAccessTokenGenerator;

        internal AuthType AuthType { get; }

        public JwtTokenCredentials(AccessKey accessKey, string serverName = null)
        {
            _restApiAccessTokenGenerator = new RestApiAccessTokenGenerator(accessKey, serverName);

            AuthType = accessKey.AuthType;
        }

        public override async Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            var uri = request.RequestUri;
            var uriWithoutPort = uri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Port, UriFormat.UriEscaped);

            var tokenString = await _restApiAccessTokenGenerator.Generate(uriWithoutPort);
            HttpRequestHeaders headers = request.Headers;
            headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            await base.ProcessHttpRequestAsync(request, cancellationToken);
        }
    }
}
