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
    public class MockRestClientHelper
    {
        public ServiceClientCredentials Credentials { get; set; } = new Mock<ServiceClientCredentials>().Object;

        public SignalRServiceRestClient GetRestClientReturnStatusCode(HttpResponseMessage response)
        {
            var mock = new Mock<HttpMessageHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
            var mockHandler = mock.Object;

            return new SignalRServiceRestClient(Credentials, new HttpClient(mockHandler), false);
        }

        public SignalRServiceRestClient GetRestClientReturnStatusCode(HttpStatusCode statusCode)
        {
            return GetRestClientReturnStatusCode(new HttpResponseMessage(statusCode));
        }
    }
}
