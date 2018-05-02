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
        [Fact]
        public void AddAzureSignalRReadsDefaultConfigurationKeyForConnectionString()
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string>
                            {
                                { "Azure:SignalR:ConnectionString", "myconnectionstring" }
                            })
                            .Build();
            var serviceProvider = services.AddSignalR()
                                          .AddAzureSignalR()
                                          .Services
                                          .AddSingleton<IConfiguration>(config)
                                          .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal("myconnectionstring", options.ConnectionString);
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
                                { "Azure:SignalR:ConnectionString", "myconnectionstring" }
                            })
                            .Build();
            var serviceProvider = services.AddSignalR()
                                          .AddAzureSignalR(o =>
                                          {
                                              o.ConnectionCount = 1;
                                          })
                                          .Services
                                          .AddSingleton<IConfiguration>(config)
                                          .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal("myconnectionstring", options.ConnectionString);
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
                                { "Azure:SignalR:ConnectionString", "myconnectionstring" }
                            })
                            .Build();
            string capturedConnectionString = null;
            var serviceProvider = services.AddSignalR()
                                          .AddAzureSignalR(o =>
                                          {
                                              capturedConnectionString = o.ConnectionString;
                                          })
                                          .Services
                                          .AddSingleton<IConfiguration>(config)
                                          .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal("myconnectionstring", options.ConnectionString);
            Assert.Equal("myconnectionstring", capturedConnectionString);
        }
    }
}