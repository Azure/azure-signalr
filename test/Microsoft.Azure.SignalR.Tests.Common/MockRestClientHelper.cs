// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

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
