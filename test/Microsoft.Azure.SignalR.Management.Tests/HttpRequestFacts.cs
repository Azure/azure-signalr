// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class HttpRequestFacts
    {
        [Fact]
        public async Task RequestAndResponse()
        {
            var httpRequest = new HttpRequest();
            var result = await httpRequest.SendAsync("http://baidu.com", null, null, HttpMethod.Get);

            Assert.Equal($"{HttpStatusCode.OK}", $"{result.StatusCode}");
        }
    }
}
