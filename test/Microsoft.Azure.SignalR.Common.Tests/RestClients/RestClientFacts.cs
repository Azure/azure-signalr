// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.RestClients
{
    public class RestClientFacts
    {
#if NET5_0_OR_GREATER

        [Fact]
        public async Task TestHttpRequestExceptionWithStatusCodeSetAsync()
        {
            var httpClientFactory = new ServiceCollection()
                .AddHttpClient(Constants.HttpClientNames.UserDefault).ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(HttpStatusCode.InsufficientStorage)).Services
                .BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            var client = new RestClient(httpClientFactory);
            var apiEndpoint = new RestApiEndpoint("https://localhost.test.com", "token");
            var exception = await Assert.ThrowsAsync<AzureSignalRRuntimeException>(() =>
            {
                return client.SendAsync(apiEndpoint, HttpMethod.Get, "", handleExpectedResponse: null);
            });
            var httpRequestException = Assert.IsType<HttpRequestException>(exception.InnerException);
            Assert.Equal(HttpStatusCode.InsufficientStorage, httpRequestException.StatusCode);
        }
#endif
    }
}
