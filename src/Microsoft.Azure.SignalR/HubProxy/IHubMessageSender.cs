// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal interface IHubMessageSender
    {
        Task<HttpResponseMessage> PostAsync(string url, string bearer, string method, object[] args, IReadOnlyList<string> excludedIds = null);

        Task<HttpResponseMessage> SendAsync(string url, string bearer, HttpMethod method);
    }
}
