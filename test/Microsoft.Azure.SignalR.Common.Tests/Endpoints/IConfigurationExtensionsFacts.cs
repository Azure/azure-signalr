// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class IConfigurationExtensionsFacts
    {
        [Theory]
        [InlineData("a")]
        [InlineData("a:primary")]
        [InlineData("secondary")]
        [InlineData("a:secondary")]
        [InlineData(":secondary")]
        public void GetEndpointsWithSuffix(string relativePath)
        {
            var connectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var connectionStringKey = "key";
            configuration[connectionStringKey] = connectionString;
            configuration[$"{connectionStringKey}:{relativePath}"] = connectionString;
            Assert.Single(configuration.GetEndpoints(connectionStringKey));
        }
    }
}