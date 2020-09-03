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

using System.Net.Http;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.RestClients
{
    public class SignalRServiceRestClientHelperFacts
    {
        private const string ProductInfo = "productInfo";
        private const string AccessKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const string Endpoint = "http://endpoint/";
        private readonly string _connectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";

        [Fact]
        public void GetCustomizedClient_RequestContainsAsrsUAFact()
        {
            var mockHelper = new MockHandlerHelper();
            string requestUri = "http://requestUri";  //The implementation of SignalRServiceRestClient doesn't pass the base URI to its member HttpClient

            var mock = mockHelper.GetVerificationHandlerMock((request, t) =>
            {
                Assert.True(request.Headers.Contains(Constants.AsrsUserAgent));
                Assert.NotNull(request.Headers.GetValues(Constants.AsrsUserAgent));
            });
            var handler = mock.Object;

            SignalRServiceRestClient restClient = new SignalRServiceRestClientHelper()
                .GetCustomizedClient(_connectionString, ProductInfo, handler);
            var httpClient = restClient.HttpClient;
            httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri));

            mockHelper.AssertHandlerUsed(mock);
            Assert.Equal(Endpoint, restClient.BaseUri.AbsoluteUri);
        }

        [Fact]
        public async void GetCustomizedClient_RequestContainsCredentials()
        {
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
            SignalRServiceRestClient restClient = new SignalRServiceRestClientHelper()
                .GetCustomizedClient(_connectionString, ProductInfo, handler);
            await restClient.HealthApi.GetHealthStatusWithHttpMessagesAsync();

            mockHelper.AssertHandlerUsed(mock);
        }

        [Fact]
        public void GetCustomiazeClient_BaseUriRightFact()
        {
            SignalRServiceRestClient restClient = new SignalRServiceRestClientHelper().GetCustomizedClient(_connectionString, ProductInfo);
            Assert.Equal(Endpoint, restClient.BaseUri.AbsoluteUri);
        }

    }
}
