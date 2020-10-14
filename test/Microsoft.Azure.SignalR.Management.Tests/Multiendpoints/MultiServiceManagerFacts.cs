// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Management.MultiEndpoints;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class MultiServiceManagerFacts
    {

        [Fact]
        public async Task CreateHubContextFacts()
        {
            ServiceEndpoint[] endpoints = FakeEndpointConstant.FakeServiceEndpoints;
            var options = new ServiceManagerOptions
            {
                ServiceEndpoints = endpoints,
                ServiceTransportType = ServiceTransportType.Persistent
            };
            var multiServiceManager = new MultiServiceManager(options, "productInfo", null);
            var serviceHubContext = await multiServiceManager.CreateHubContextAsync("abc");
            await serviceHubContext.DisposeAsync();
        }


    }
}
