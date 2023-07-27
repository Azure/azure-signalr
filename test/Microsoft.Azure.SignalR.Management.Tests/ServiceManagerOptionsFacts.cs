// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
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

        [Fact]
        public void OptionsBindingFact()
        {
            var configuration = new ConfigurationBuilder()
                               .AddInMemoryCollection()
                               .Build();
            var connectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
            configuration["ApplicationName"] = "applicationName";
            configuration["ConnectionCount"] = "3";
            configuration["ConnectionString"] = connectionString;
            configuration["ServiceTransportType"] = "Persistent";
            configuration["HttpClientTimeout"] = "00:00:10";

            var options = new ServiceManagerOptions();
            configuration.Bind(options);
            Assert.Equal("applicationName", options.ApplicationName);
            Assert.Equal(3, options.ConnectionCount);
            Assert.Equal(connectionString, options.ConnectionString);
            Assert.Equal(ServiceTransportType.Persistent, options.ServiceTransportType);
            Assert.Equal(TimeSpan.FromSeconds(10), options.HttpClientTimeout);
        }
    }
}
