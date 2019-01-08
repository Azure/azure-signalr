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
    internal static class HttpClientFactory
    {
        private static readonly IHttpClientFactory _clientFactory;

        static HttpClientFactory()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            var services = serviceCollection.BuildServiceProvider();

            _clientFactory = services.GetRequiredService<IHttpClientFactory>();
        }

        public static HttpClient CreateClient()
        {
            return _clientFactory.CreateClient();
        }

    }
}
