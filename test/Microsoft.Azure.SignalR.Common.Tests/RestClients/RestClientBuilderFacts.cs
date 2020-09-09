// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.RestClients
{
    public class RestClientBuilderFacts
    {
        private const string AccessKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const string Endpoint = "http://endpoint/";
        private readonly string _connectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";

        [Fact]
        public void RequestContainsAsrsUAFact()
        {
            string productInfo = "productInfo";

            void action(RestClientBuilder b) => b.WithProductInfo(productInfo);

            void assertion(HttpRequestMessage request, CancellationToken t)
            {
                Assert.True(request.Headers.Contains(Constants.AsrsUserAgent));
                Assert.NotNull(request.Headers.GetValues(Constants.AsrsUserAgent));
            }

            TestRestClientBuilder(assertion, action);
        }

        [Fact]
        public void RequestContainsCredentials()
        {
            void assertion(HttpRequestMessage request, CancellationToken t)
            {
                var authHeader = request.Headers.Authorization;
                string scheme = authHeader.Scheme;
                string parameter = authHeader.Parameter;

                Assert.Equal("Bearer", scheme);
                Assert.NotNull(parameter);
            }
            TestRestClientBuilder(assertion, null);
        }

        [Fact]
        public void GetCustomiazeClient_BaseUriRightFact()
        {
            RestClientBuilder restClientBuilder = new RestClientBuilder(_connectionString);
            var restClient = restClientBuilder.Build();

            Assert.Equal(Endpoint, restClient.BaseUri.AbsoluteUri);
        }

        private async void TestRestClientBuilder(Action<HttpRequestMessage, CancellationToken> assertion, Action<RestClientBuilder> action)
        {
            var mockHelper = new MockHandlerHelper();
            var mock = mockHelper.GetVerificationHandlerMock(assertion);
            var handler = mock.Object;
            RestClientBuilder restClientBuilder = new RestClientBuilder(_connectionString)
                .WithHandler(handler);

            action?.Invoke(restClientBuilder);
            using var restClient = restClientBuilder.Build();
            await restClient.HealthApi.GetHealthStatusWithHttpMessagesAsync();

            mockHelper.AssertHandlerUsed(mock);
        }

    }
}