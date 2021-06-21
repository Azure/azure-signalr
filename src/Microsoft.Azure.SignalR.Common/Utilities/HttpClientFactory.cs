// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    internal static class HttpClientFactory
    {
        public static IHttpClientFactory Instance { get; } = new ServiceCollection().AddHttpClient()
                                                                                    .BuildServiceProvider()
                                                                                    .GetRequiredService<IHttpClientFactory>();
    }
}