// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal class TestRootHandler : DelegatingHandler
    {
        private readonly Action<HttpRequestMessage, CancellationToken> _callback;
        private readonly HttpStatusCode _code;
        private readonly string _content;

        public TestRootHandler(HttpStatusCode code)
        {
            _code = code;
        }

        public TestRootHandler(HttpStatusCode code, string content) : this(code)
        {
            _content = content;
        }

        public TestRootHandler(Action<HttpRequestMessage, CancellationToken> callback)
            : this(HttpStatusCode.OK)
        {
            _callback = callback;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(false);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            //to avoid possible retry policy which dispose content, create new content each time
            var response = new HttpResponseMessage(_code)
            {
                RequestMessage = request
            };
            if (_content != null)
            {
                response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(_content));
            }
            _callback?.Invoke(request, cancellationToken);
            return Task.FromResult(response);
        }
    }
}
