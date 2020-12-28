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
        [InlineData("a") ]
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

        [Fact]
        public void GetEndpoints_IncludeNoSuffixEndpoint()
        {
            var count = 3;
            var connectionStrings = FakeEndpointUtils.GetFakeConnectionString(count).ToArray();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var connectionStringKey = "key";
            configuration[connectionStringKey] = connectionStrings[0];
            configuration[$"{connectionStringKey}:a"] = connectionStrings[1];
            configuration[$"{connectionStringKey}:a:primary"] = connectionStrings[2];
            Assert.Equal(count, configuration.GetEndpoints(connectionStringKey, true).Count());
        }
    }
}