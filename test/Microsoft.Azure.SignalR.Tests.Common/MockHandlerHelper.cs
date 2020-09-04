// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class MockHandlerHelper
    {
        public Mock<DelegatingHandler> GetVerificationHandlerMock(Action<HttpRequestMessage, CancellationToken> callback, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var mock = new Mock<DelegatingHandler>();
            mock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback(callback)
                .ReturnsAsync(new HttpResponseMessage(statusCode));
            return mock;
        }

        public void AssertHandlerUsed(Mock<DelegatingHandler> mock)
        {
            mock.Protected()
                .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }
    }
}
