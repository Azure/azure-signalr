// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Moq;
using Moq.Protected;

namespace Microsoft.Azure.SignalR.Tests
{
    public class TestRestClient : SignalRServiceRestClient
    {
        public TestRestClient(HttpResponseMessage response) : base(new Mock<ServiceClientCredentials>().Object, GetTestHandler(response), false) { }

        public TestRestClient(HttpStatusCode code) : base(new Mock<ServiceClientCredentials>().Object, GetTestHandler(new HttpResponseMessage(code)), false) { }

        private static HttpClient GetTestHandler(HttpResponseMessage response)
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
            var mockHandler = mock.Object;
            return new HttpClient(mockHandler);
        }
    }
}
