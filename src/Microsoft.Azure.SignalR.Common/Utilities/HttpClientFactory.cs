// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    internal class HttpClientFactory
    {
        internal static IHttpClientFactory ClientFactory { get; }

        static HttpClientFactory()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient();
            var services = serviceCollection.BuildServiceProvider();

            ClientFactory = services.GetRequiredService<IHttpClientFactory>();
        }

        public static HttpClient CreateClient()
        {
            return ClientFactory.CreateClient();
        }

    }
}
