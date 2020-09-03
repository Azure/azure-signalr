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
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Rest;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.RestClients
{
    public class HealthApiFacts
    {
        [Fact]
        public async void IsServiceHealthyReturnTrue()
        {
            var signalRServiceRestClient = new MockRestClientHelper()
                .GetRestClientReturnStatusCode(HttpStatusCode.OK);
            var healthApi = new HealthApi(signalRServiceRestClient);

            var operationResponse = await healthApi.GetHealthStatusWithHttpMessagesAsync();

            Assert.Equal(HttpStatusCode.OK, operationResponse.Response.StatusCode);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Conflict)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async void IsServiceHealthyThrowException(HttpStatusCode statusCode)
        //always throw exception when status code != 200
        {
            string contentString = "response content";
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(contentString));
            HttpResponseMessage message = new HttpResponseMessage
            {
                Content = content,
                StatusCode = statusCode
            };
            var signalRServiceRestClient = new MockRestClientHelper()
                .GetRestClientReturnStatusCode(message);
            var healthApi = new HealthApi(signalRServiceRestClient);

            Task func() => healthApi.GetHealthStatusWithHttpMessagesAsync();

            HttpOperationException exception = await Assert.ThrowsAsync<HttpOperationException>(func);
            Assert.Equal(statusCode, exception.Response.StatusCode);
            Assert.Equal(contentString, exception.Response.Content);
        }

    }
}
