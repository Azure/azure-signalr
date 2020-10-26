// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Rest;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.RestClients
{
    public class HealthApiFacts
    {
        private const string UserAgent = "userAgent";
        private const string AccessKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const string Endpoint = "http://endpoint/";
        private static readonly string _connectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";
        private static readonly ServiceEndpoint serviceEndpoint = new ServiceEndpoint(_connectionString);
        [Fact]
        public async void IsServiceHealthyReturnTrue()
        {
            using var signalRServiceRestClient = new TestRestClientFactory(UserAgent, HttpStatusCode.OK).Create(serviceEndpoint);
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
            using var signalRServiceRestClient = new TestRestClientFactory(UserAgent, statusCode, contentString).Create(serviceEndpoint);
            var healthApi = new HealthApi(signalRServiceRestClient);

            HttpOperationException exception = await Assert.ThrowsAsync<HttpOperationException>(() => healthApi.GetHealthStatusWithHttpMessagesAsync());
            Assert.Equal(statusCode, exception.Response.StatusCode);
            Assert.Equal(contentString, exception.Response.Content);
        }
    }
}
