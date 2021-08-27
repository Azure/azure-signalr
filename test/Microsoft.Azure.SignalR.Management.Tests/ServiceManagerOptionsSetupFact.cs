// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceManagerOptionsSetupFactF
    {
        [Fact]
        public void ConfigureConnectionStringBasedServiceEndpoint_WithoutSingleConnectionString()
        {
            var connectionStrings = FakeEndpointUtils.GetFakeConnectionString(2);
            var names = new string[] { "First", "Second" };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            foreach (var (name, connectionString) in names.Zip(connectionStrings))
            {
                configuration[$"{Constants.Keys.AzureSignalREndpointsKey}:{name}"] = connectionString;
            }
            var optionsSetup = new ServiceManagerOptionsSetup(SingletonAzureComponentFactory.Instance, configuration);
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
        public void ConfigureConnectioStringBasedServiceEndpoint_WithSingleConnectionString()
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
            var optionsSetup = new ServiceManagerOptionsSetup(SingletonAzureComponentFactory.Instance, configuration);
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
            var setup = new ServiceManagerOptionsSetup(SingletonAzureComponentFactory.Instance, configuration);
            setup.Configure(options);
            Assert.Equal(app, options.ApplicationName);
            Assert.Equal(connStr, options.ConnectionString);
            Assert.Equal(endpoints, options.ServiceEndpoints);
        }

        [Fact]
        public void TestIdentityBasedServiceEndpoints()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var setup = new ServiceManagerOptionsSetup(SingletonAzureComponentFactory.Instance, config);
            var uri1 = "http://localhost:88";
            var uri2 = "http://localhost:99";
            config["Azure:SignalR:Connection:ServiceUri"] = uri1;
            config["Azure:SignalR:Endpoints:eastus:ServiceUri"] = uri2;
            var options = new ServiceManagerOptions();
            setup.Configure(options);

            Assert.Equal(2, options.ServiceEndpoints.Length);
            Assert.Contains(options.ServiceEndpoints, e => e.Endpoint == uri1);
            Assert.Contains(options.ServiceEndpoints, e => e.Endpoint == uri2);
        }
    }
}