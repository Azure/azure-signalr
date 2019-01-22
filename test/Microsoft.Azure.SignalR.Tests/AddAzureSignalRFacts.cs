// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class AddAzureSignalRFacts
    {
        private const string CustomValue = "Endpoint=https://customconnectionstring;AccessKey=1";
        private const string DefaultValue = "Endpoint=https://defaultconnectionstring;AccessKey=1";
        private const string SecondaryValue = "Endpoint=https://secondaryconnectionstring;AccessKey=1";

        [Fact]
        public void AddAzureSignalRReadsDefaultConfigurationKeyForConnectionString()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Azure:SignalR:ConnectionString", DefaultValue}
                })
                .Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR()
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal(DefaultValue, options.ConnectionString);
            Assert.Equal(5, options.ConnectionCount);
            Assert.Equal(TimeSpan.FromHours(1), options.AccessTokenLifetime);
            Assert.Null(options.ClaimsProvider);
        }

        [Fact]
        public void AddAzureUsesDefaultConnectionStringIfSpecifiedAndOptionsOverridden()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Azure:SignalR:ConnectionString", DefaultValue}
                })
                .Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR(o => { o.ConnectionCount = 1; })
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal(DefaultValue, options.ConnectionString);
            Assert.Equal(1, options.ConnectionCount);
            Assert.Equal(TimeSpan.FromHours(1), options.AccessTokenLifetime);
            Assert.Null(options.ClaimsProvider);
        }

        [Fact]
        public void AddAzureReadsConnectionStringFirst()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Azure:SignalR:ConnectionString", DefaultValue}
                })
                .Build();
            string capturedConnectionString = null;
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR(o => { capturedConnectionString = o.ConnectionString; })
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal(DefaultValue, options.ConnectionString);
            Assert.Equal(DefaultValue, capturedConnectionString);
        }

        [Theory]
        [InlineData(CustomValue, null, null, CustomValue)]
        [InlineData(CustomValue, DefaultValue, SecondaryValue, CustomValue)]
        [InlineData(null, DefaultValue, SecondaryValue, DefaultValue)]
        [InlineData(null, null, SecondaryValue, SecondaryValue)]
        public void AddAzureSignalRLoadConnectionStringOrder(string customValue, string defaultValue,
            string secondaryValue, string expected)
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Azure:SignalR:ConnectionString", defaultValue},
                    {"ConnectionStrings:Azure:SignalR:ConnectionString", secondaryValue}
                })
                .Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR(o =>
                {
                    if (customValue != null)
                    {
                        o.ConnectionString = customValue;
                    }
                })
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal(expected, options.ConnectionString);
        }

        [Theory]
        [InlineData(CustomValue, null, null, CustomValue, 0)]
        [InlineData(CustomValue, DefaultValue, SecondaryValue, CustomValue, 2)]
        [InlineData(null, DefaultValue, SecondaryValue, DefaultValue, 2)]
        [InlineData(null, null, SecondaryValue, null, 1)]
        public void AddAzureSignalRReadServiceEndpointsFromConfig(string customValue, string defaultValue,
            string secondaryValue, string expected, int expectedCount)
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Azure:SignalR:ConnectionString", defaultValue},
                    {"Azure:SignalR:ConnectionString:1:secondary", secondaryValue},
                })
                .Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR(o =>
                {
                    if (customValue != null)
                    {
                        o.ConnectionString = customValue;
                    }
                })
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal(expected, options.ConnectionString);
            Assert.Equal(expectedCount, options.Endpoints.Length);
        }

        [Fact]
        public void AddAzureSignalRCustomizeEndpointsOverridesConfigValue()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Azure:SignalR:ConnectionString", DefaultValue},
                    {"Azure:SignalR:ConnectionString:1:secondary", SecondaryValue},
                })
                .Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR(o =>
                {
                    o.Endpoints = new ServiceEndpoint[]
                    {
                        new ServiceEndpoint(CustomValue, EndpointType.Primary),
                        new ServiceEndpoint(CustomValue, EndpointType.Secondary),
                        new ServiceEndpoint(SecondaryValue, EndpointType.Secondary),
                    };
                })
                .Services
                .AddSingleton<IConfiguration>(config)
                .AddLogging()
                .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal(DefaultValue, options.ConnectionString);
            Assert.Equal(3, options.Endpoints.Length);
            Assert.Equal(EndpointType.Primary, options.Endpoints[0].EndpointType);
            Assert.Equal(CustomValue, options.Endpoints[0].ConnectionString);
            Assert.Equal(SecondaryValue, options.Endpoints[2].ConnectionString);

            var endpointManager = serviceProvider.GetRequiredService<IServiceEndpointManager>();
            var endpoints = endpointManager.GetAvailableEndpoints().ToArray();
            Assert.Equal(3, endpoints.Length);
            Assert.Equal(EndpointType.Primary, endpoints[0].EndpointType);
            Assert.Equal(DefaultValue, endpoints[0].ConnectionString);
            Assert.Equal(EndpointType.Primary, endpoints[1].EndpointType);
            Assert.Equal(CustomValue, endpoints[1].ConnectionString);
            Assert.Equal(EndpointType.Secondary, endpoints[2].EndpointType);
            Assert.Equal(SecondaryValue, endpoints[2].ConnectionString);
        }
    }
}