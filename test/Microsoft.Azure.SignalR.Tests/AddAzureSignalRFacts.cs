// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class AddAzureSignalRFacts
    {
        private const string CustomValue = "customconnectionstring";
        private const string DefaultValue = "defaultconnectionstring";
        private const string SecondaryValue = "secondaryconnectionstring";

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
    }
}