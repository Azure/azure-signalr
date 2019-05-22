// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    public class AddAzureSignalRFacts : VerifiableLoggedTest
    {
        private const string CustomValue = "Endpoint=https://customconnectionstring;AccessKey=1";
        private const string DefaultValue = "Endpoint=https://defaultconnectionstring;AccessKey=1";
        private const string SecondaryValue = "Endpoint=https://secondaryconnectionstring;AccessKey=1";

        public AddAzureSignalRFacts(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void AddAzureSignalRReadsDefaultConfigurationKeyForConnectionString()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
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
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(DefaultValue, options.ConnectionString);
                Assert.Equal(Constants.DefaultInitConnectionCountPerHub, options.ConnectionCount);
                Assert.Equal(TimeSpan.FromHours(1), options.AccessTokenLifetime);
                Assert.Null(options.ClaimsProvider);
            }
        }

        [Fact]
        public void AddAzureUsesDefaultConnectionStringIfSpecifiedAndOptionsOverridden()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:ConnectionString", DefaultValue}
                    })
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:ConnectionString", CustomValue}
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddAzureSignalR(o =>
                    {
                        o.ConnectionCount = 1;
                    })
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(CustomValue, options.ConnectionString);
                Assert.Equal(1, options.ConnectionCount);
                Assert.Equal(TimeSpan.FromHours(1), options.AccessTokenLifetime);
                Assert.Null(options.ClaimsProvider);
            }
        }

        [Fact]
        public void AddAzureReadsConnectionStringFirst()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:ConnectionString", DefaultValue},
                        {"Azure:SignalR:StickyServerMode", "invalid" }
                    })
                    .Build();
                string capturedConnectionString = null;
                var serviceProvider = services.AddSignalR()
                    .AddAzureSignalR(o => { capturedConnectionString = o.ConnectionString; })
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(DefaultValue, options.ConnectionString);
                Assert.Equal(DefaultValue, capturedConnectionString);
            }
        }


        [Theory]
        [InlineData(null, ServerStickyMode.Disabled)]
        [InlineData("invalid", ServerStickyMode.Disabled)]
        [InlineData("disabled", ServerStickyMode.Disabled)]
        //[InlineData("preferred", ServerStickyMode.Preferred)]
        //[InlineData("Preferred", ServerStickyMode.Preferred)]
        [InlineData("required", ServerStickyMode.Required)]
        public void AddAzureReadsSickyServerModeFromConfigurationFirst(string modeFromConfig, ServerStickyMode expected)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"Azure:SignalR:ServerStickyMode", modeFromConfig }
                    })
                    .Build();
                ServerStickyMode capturedMode = ServerStickyMode.Disabled;
                var serviceProvider = services.AddSignalR()
                    .AddAzureSignalR(o => 
                    {
                        capturedMode = o.ServerStickyMode;
                    })
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(expected, capturedMode);
                Assert.Equal(expected, options.ServerStickyMode);
            }
        }

        [Theory]
        [InlineData(CustomValue, null, null, CustomValue)]
        [InlineData(CustomValue, DefaultValue, SecondaryValue, CustomValue)]
        [InlineData(null, DefaultValue, SecondaryValue, DefaultValue)]
        [InlineData(null, null, SecondaryValue, SecondaryValue)]
        public void AddAzureSignalRLoadConnectionStringOrder(string customValue, string defaultValue,
            string secondaryValue, string expected)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
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
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(expected, options.ConnectionString);
            }
        }

        [Theory]
        [InlineData(CustomValue, null, null, CustomValue)]
        [InlineData(CustomValue, DefaultValue, SecondaryValue, CustomValue)]
        [InlineData(null, DefaultValue, SecondaryValue, DefaultValue)]
        public void AddAzureSignalRReadServiceEndpointsFromConfig(string customValue, string defaultValue,
            string secondaryValue, string expected)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
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
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(expected, options.ConnectionString);
                if (secondaryValue != null)
                {
                    // Endpoints option is always separate from the ConnectionString options
                    Assert.Single(options.Endpoints);
                    Assert.Equal(secondaryValue, options.Endpoints[0].ConnectionString);
                }

                // Endpoints from Endpoints and ConnectionString config are merged inside the EndpointManager
                var endpoints = serviceProvider.GetRequiredService<IServiceEndpointManager>().Endpoints;
                if (secondaryValue == null)
                {
                    // When no other connection string is defined, endpoints value is always connection string value
                    Assert.Single(endpoints);
                    Assert.Equal(expected, endpoints[0].ConnectionString);
                }
                else
                {
                    // When default connection string is not specified
                    if (defaultValue == null && customValue == null)
                    {
                        Assert.Single(endpoints);
                        Assert.Equal(expected, endpoints[0].ConnectionString);
                    }
                    else
                    {
                        Assert.Equal(2, endpoints.Length);
                        Assert.Equal(expected, endpoints[0].ConnectionString);
                        Assert.Equal(secondaryValue, endpoints[1].ConnectionString);
                    }
                }
            }
        }

        [Theory]
        [InlineData(null, null, SecondaryValue, null)]
        public void AddAzureSignalRWithOnlySecondaryValueThrows(string customValue, string defaultValue,
            string secondaryValue, string expected)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
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
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(expected, options.ConnectionString);
                if (secondaryValue != null)
                {
                    // Endpoints option is always separate from the ConnectionString options
                    Assert.Single(options.Endpoints);
                    Assert.Equal(secondaryValue, options.Endpoints[0].ConnectionString);
                }

                // Endpoints from Endpoints and ConnectionString config are merged inside the EndpointManager
                Assert.Throws<AzureSignalRNoPrimaryEndpointException>(() =>
                    serviceProvider.GetRequiredService<IServiceEndpointManager>());
            }
        }

        [Theory]
        [InlineData(DefaultValue, null, DefaultValue, 3)]
        [InlineData(DefaultValue, CustomValue, CustomValue, 2)]
        [InlineData(null, CustomValue, CustomValue, 2)]
        [InlineData(null, null, null, 2)]
        public void AddAzureSignalRCustomizeEndpointsOverridesConfigValue(string defaultValue, string customValue, string expected, int expectedCount)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"Azure:SignalR:ConnectionString", defaultValue},
                        {"Azure:SignalR:ConnectionString:1:secondary", SecondaryValue},
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddAzureSignalR(o =>
                    {
                        if (customValue != null)
                        {
                            o.ConnectionString = customValue;
                        }

                        // The final endpoints merges this customize endpoints and connection string if specified
                        o.Endpoints = new[]
                        {
                            new ServiceEndpoint(CustomValue, EndpointType.Primary),
                            new ServiceEndpoint(CustomValue, EndpointType.Secondary),
                            new ServiceEndpoint(SecondaryValue, EndpointType.Secondary),
                        };
                    })
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(expected, options.ConnectionString);
                Assert.Equal(3, options.Endpoints.Length);
                Assert.Equal(EndpointType.Primary, options.Endpoints[0].EndpointType);
                Assert.Equal(CustomValue, options.Endpoints[0].ConnectionString);
                Assert.Equal(SecondaryValue, options.Endpoints[2].ConnectionString);

                var endpointManager = serviceProvider.GetRequiredService<IServiceEndpointManager>();

                var endpoints = endpointManager.Endpoints;

                Assert.Equal(expectedCount, endpoints.Length);

                Assert.Contains(endpoints,
                    s => s.ConnectionString == CustomValue && s.EndpointType == EndpointType.Primary);

                // Secondary is overwritten by primary
                Assert.DoesNotContain(endpoints,
                    s => s.ConnectionString == CustomValue && s.EndpointType == EndpointType.Secondary);

                Assert.Contains(endpoints,
                    s => s.ConnectionString == SecondaryValue && s.EndpointType == EndpointType.Secondary);

                if (expected != null)
                {
                    Assert.Contains(endpoints,
                        s => s.ConnectionString == expected && s.EndpointType == EndpointType.Primary);
                }
            }
        }
    }
}