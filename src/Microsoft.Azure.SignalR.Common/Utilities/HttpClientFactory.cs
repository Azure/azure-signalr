// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    internal static class HttpClientFactory
    {
        private static readonly ConditionalWeakTable<IWebProxy, IHttpClientFactory> ProxyFactories = new();

        public static IHttpClientFactory Instance { get; } = new ServiceCollection().AddHttpClient()
                                                                                    .BuildServiceProvider()
                                                                                    .GetRequiredService<IHttpClientFactory>();

        public static IHttpClientFactory GetInstance(IWebProxy proxy)
        {
            return ProxyFactories.GetValue(proxy, p => new ServiceCollection()
                .AddHttpClient(Constants.HttpClientNames.UserDefault)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { Proxy = p })
                .Services.BuildServiceProvider()
                .GetRequiredService<IHttpClientFactory>());
        }
    }
}
