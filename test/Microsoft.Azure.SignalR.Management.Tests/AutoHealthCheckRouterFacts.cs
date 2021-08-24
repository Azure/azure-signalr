// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class AutoHealthCheckRouterFacts
    {
        [Fact]
        public void TestAutoHealthCheckRouterWithSingleEndpoint()
        {
            var router = new AutoHealthCheckRouter();
            var endpoints = new ServiceEndpoint[] { new ServiceEndpoint(FakeEndpointUtils.GetFakeConnectionString(1).First()) { Online = false } };
            Assert.Equal(endpoints.Single(), router.GetNegotiateEndpoint(null, endpoints));
        }
    }
}
