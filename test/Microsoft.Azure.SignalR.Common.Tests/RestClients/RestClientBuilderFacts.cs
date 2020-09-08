// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.RestClients
{
    public class RestClientBuilderFacts
    {
        private const string ProductInfo = "productInfo";
        private const string AccessKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const string Endpoint = "http://endpoint/";
        private readonly string _connectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";

        [Fact]
        public void RequestContainsAsrsUAFact()
        {
            ServiceEndpoint serviceEndpoint = new ServiceEndpoint(_connectionString);
            var mockHelper = new MockHandlerHelper();
            string requestUri = "http://requestUri";  //The implementation of SignalRServiceRestClient doesn't pass the base URI to its member HttpClient

            var mock = mockHelper.GetVerificationHandlerMock((request, t) =>
            {
                Assert.True(request.Headers.Contains(Constants.AsrsUserAgent));
                Assert.NotNull(request.Headers.GetValues(Constants.AsrsUserAgent));
            });
            var handler = mock.Object;

            RestClientBuilder restClientBuilder = new RestClientBuilder()
                .WithProductInfo(ProductInfo)
                .WithServiceEndpoint(serviceEndpoint)
                .WithHandler(new DelegatingHandler[] { handler });
            var restClient = restClientBuilder.Build();
            var httpClient = restClient.HttpClient;
            httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri));

            mockHelper.AssertHandlerUsed(mock);
        }

        [Fact]
        public async void RequestContainsCredentials()
        {
            ServiceEndpoint serviceEndpoint = new ServiceEndpoint(_connectionString);
            var mockHelper = new MockHandlerHelper();
            var mock = mockHelper.GetVerificationHandlerMock((request, t) =>
            {
                var authHeader = request.Headers.Authorization;
                string scheme = authHeader.Scheme;
                string parameter = authHeader.Parameter;

                Assert.Equal("Bearer", scheme);
                Assert.NotNull(parameter);
            });
            var handler = mock.Object;

            RestClientBuilder restClientBuilder = new RestClientBuilder()
                .WithServiceEndpoint(serviceEndpoint)
                .WithHandler(new DelegatingHandler[] { handler });
            var restClient = restClientBuilder.Build();
            await restClient.HealthApi.GetHealthStatusWithHttpMessagesAsync();

            mockHelper.AssertHandlerUsed(mock);
        }

        [Fact]
        public void GetCustomiazeClient_BaseUriRightFact()
        {
            ServiceEndpoint serviceEndpoint = new ServiceEndpoint(_connectionString);

            RestClientBuilder restClientBuilder = new RestClientBuilder()
                .WithProductInfo(ProductInfo)
                .WithServiceEndpoint(serviceEndpoint);
            var restClient = restClientBuilder.Build();

            Assert.Equal(Endpoint, restClient.BaseUri.AbsoluteUri);
        }

    }
}
