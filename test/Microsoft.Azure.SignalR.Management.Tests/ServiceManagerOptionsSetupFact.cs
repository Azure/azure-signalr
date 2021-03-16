// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceManagerOptionsSetupFactF
    {
        [Fact]
        public void ConfigureServiceEndpoint_WithoutConnectionString()
        {
            var connectionStrings = FakeEndpointUtils.GetFakeConnectionString(2);
            var names = new string[] { "First", "Second" };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            foreach (var (name, connectionString) in names.Zip(connectionStrings))
            {
                configuration[$"{Constants.Keys.AzureSignalREndpointsKey}:{name}"] = connectionString;
            }
            var optionsSetup = new ServiceManagerOptionsSetup(configuration);
            var options = new ServiceManagerOptions();

            optionsSetup.Configure(options);

            var actualEndpoints = options.ServiceEndpoints;
            foreach (var (name, connectionString) in names.Zip(connectionStrings))
            {
                Assert.Contains(new ServiceEndpoint(name, connectionString), actualEndpoints);
            }
            Assert.Null(options.ConnectionString);
        }

        [Fact]
        public void ConfigureServiceEndpoint_WithConnectionString()
        {
            var connectionStrings = FakeEndpointUtils.GetFakeConnectionString(2);
            var names = new string[] { "First", "Second" };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            foreach (var (name, connStr) in names.Zip(connectionStrings))
            {
                configuration[$"{Constants.Keys.AzureSignalREndpointsKey}:{name}"] = connStr;
            }
            var connectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
            configuration[Constants.Keys.ConnectionStringDefaultKey] = connectionString;
            var optionsSetup = new ServiceManagerOptionsSetup(configuration);
            var options = new ServiceManagerOptions();

            optionsSetup.Configure(options);

            var actualEndpoints = options.ServiceEndpoints;
            foreach (var (name, connStr) in names.Zip(connectionStrings))
            {
                Assert.Contains(new ServiceEndpoint(name, connStr), actualEndpoints);
            }
            Assert.Equal(connectionString, options.ConnectionString);
        }

        [Fact]
        public void EmptyConfiguration_NotCleanOriginalValue()
        {
            const string app = "App";
            var connStr = FakeEndpointUtils.GetFakeConnectionString(1).Single();
            var endpoints = FakeEndpointUtils.GetFakeEndpoint(2).ToArray();
            var options = new ServiceManagerOptions
            {
                ApplicationName = app,
                ConnectionString = connStr,
                ServiceEndpoints = endpoints
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var setup = new ServiceManagerOptionsSetup(configuration);
            setup.Configure(options);
            Assert.Equal(app, options.ApplicationName);
            Assert.Equal(connStr, options.ConnectionString);
            Assert.Equal(endpoints, options.ServiceEndpoints);
        }
    }
}