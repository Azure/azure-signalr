// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;

namespace Microsoft.Azure.SignalR.E2ETests.Management
{
    public class ServiceManagerE2EFacts
    {
        [ConditionalFact]
        [SkipIfConnectionStringNotPresent]
        public async Task CheckServiceHealthTest()
        {
            var builder = new ServiceManagerBuilder();
            var serviceManager = builder
                .WithOptions(o =>
                {
                    o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                })
                .BuildServiceManager();

            var isHealthy = await serviceManager.IsServiceHealthy(default);
            Assert.True(isHealthy);
        }
    }
}
