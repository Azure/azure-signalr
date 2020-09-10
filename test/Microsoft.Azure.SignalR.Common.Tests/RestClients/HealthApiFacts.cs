// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
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
            using var signalRServiceRestClient = new TestRestClient(HttpStatusCode.OK);
            var healthApi = new HealthApi(signalRServiceRestClient);

            var operationResponse = await healthApi.GetHealthStatusWithHttpMessagesAsync();

            Assert.Equal(HttpStatusCode.OK, operationResponse.Response.StatusCode);
        }

        [Theory]
        [InlineData(HttpStatusCode.ServiceUnavailable)] //will retry many times
        [InlineData(HttpStatusCode.GatewayTimeout)]  //will retry many times
        [InlineData(HttpStatusCode.BadRequest)]  //won't retry
        [InlineData(HttpStatusCode.Conflict)]   //won't retry
        public async void IsServiceHealthyThrowException(HttpStatusCode statusCode)
        //always throw exception when status code != 200
        {
            string contentString = "response content";
            using var signalRServiceRestClient = new TestRestClient(statusCode, contentString);
            var healthApi = new HealthApi(signalRServiceRestClient);

            Task action() => healthApi.GetHealthStatusWithHttpMessagesAsync();

            HttpOperationException exception = await Assert.ThrowsAsync<HttpOperationException>(action);
            Assert.Equal(statusCode, exception.Response.StatusCode);
            Assert.Equal(contentString, exception.Response.Content);
        }
    }
}
