// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.Management
{
    internal static class HttpRequestHelper
    {
        private static readonly IHttpClientFactory _clientFactory;

        static HttpRequestHelper()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            var services = serviceCollection.BuildServiceProvider();

            _clientFactory = services.GetRequiredService<IHttpClientFactory>();
        }

        public static Task<HttpResponseMessage> SendAsync(string url, PayloadMessage payload, string tokenString, HttpMethod httpMethod)
        {
            var request = new HttpRequestMessage(httpMethod, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenString);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            return _clientFactory.CreateClient().SendAsync(request);
        }
    }
}
