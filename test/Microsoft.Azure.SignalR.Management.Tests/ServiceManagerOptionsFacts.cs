// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceManagerOptionsFacts
    {
        [Fact]
        public void ForbidMultipleEndpointsInTransientModeFact()
        {
            Assert.Throws<NotImplementedException>(() => new ServiceManagerOptions
            {
                ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single(),
                ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(1).ToArray()
            }.ValidateOptions());

            Assert.Throws<NotImplementedException>(() => new ServiceManagerOptions
            {
                ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(2).ToArray()
            }.ValidateOptions());
        }

        [Fact]
        public void AllowSingleEndpointInTransientModeFact()
        {
            new ServiceManagerOptions { ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(1).ToArray() }.ValidateOptions();
        }
    }
}
