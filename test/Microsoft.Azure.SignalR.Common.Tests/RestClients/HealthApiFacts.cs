// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            var signalRServiceRestClient = new TestRestClient(HttpStatusCode.OK);
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
            var signalRServiceRestClient = new TestRestClient(message);
            var healthApi = new HealthApi(signalRServiceRestClient);

            Task func() => healthApi.GetHealthStatusWithHttpMessagesAsync();

            HttpOperationException exception = await Assert.ThrowsAsync<HttpOperationException>(func);
            Assert.Equal(statusCode, exception.Response.StatusCode);
            Assert.Equal(contentString, exception.Response.Content);
        }

    }
}
