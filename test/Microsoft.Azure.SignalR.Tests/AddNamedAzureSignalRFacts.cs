// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    public class AddNamedAzureSignalRFacts : VerifiableLoggedTest
    {
        private const string CustomValue = "Endpoint=https://customconnectionstring;AccessKey=1";
        private const string DefaultValue = "Endpoint=https://defaultconnectionstring;AccessKey=1";
        private const string SecondaryValue = "Endpoint=https://secondaryconnectionstring;AccessKey=1";
        private const string ConfigFile = "testappsettings.json";

        public AddNamedAzureSignalRFacts(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void AddNamedAzureSignalRReadsFromConnectionStringsCorrectly()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"ConnectionStrings:s1", DefaultValue},
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s1")
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(DefaultValue, options.ConnectionString);
            }
        }

        [Fact]
        public void AddNamedAzureSignalRConnectionStringCanBeSetInCode()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"ConnectionStrings:s1", DefaultValue},
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s1", s => s.ConnectionString = CustomValue)
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(CustomValue, options.ConnectionString);
            }
        }

        [Fact]
        public void AddNamedAzureSignalRConnectionNameWinsOverConfigSection()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:ConnectionString", SecondaryValue},
                    {"Azure:SignalR:s1:ConnectionString", DefaultValue},
                    {"ConnectionStrings:s1", CustomValue},
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s1")
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(CustomValue, options.ConnectionString);
            }
        }

        [Fact]
        public void AddNamedAzureSignalRReadsDefaultConfigurationKeyForConnectionString()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:name1:ConnectionString", DefaultValue},
                    {"Azure:SignalR:name1:ApplicationName", "Application1"}
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("name1")
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(DefaultValue, options.ConnectionString);
                Assert.Equal("Application1", options.ApplicationName);
                Assert.Equal(5, options.InitialHubServerConnectionCount);
                Assert.Equal(TimeSpan.FromHours(1), options.AccessTokenLifetime);
                Assert.Null(options.ClaimsProvider);
            }
        }

        [Fact]
        public void AddNamedAzureSignalRReadsInvalidCongifurationThrows()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("name1", o =>
                    {
                        o.InitialHubServerConnectionCount = 15;
                        o.MaxHubServerConnectionCount = 3;
                    })
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var ex = Assert.Throws<AzureSignalRInvalidServiceOptionsException>(() => serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value);
                Assert.Equal("Property 'MaxHubServerConnectionCount' value should be >= 15.", ex.Message);
            }
        }

        [Fact]
        public void AddNamedAzureSignalRIgnoreDefaultConnectionString()
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
                    {"ConnectionStrings:Azure:SignalR:ConnectionString", SecondaryValue}
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("name1")
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;
                Assert.Null(options.ConnectionString);
            }
        }

        [Fact]
        public void AddNamedAzureSignalRIgnoreDefaultConnectionStringDefaultOneWins()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"ConnectionStrings:Azure:SignalR:name1:ConnectionString", DefaultValue}
                    })
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:name1:ConnectionString", CustomValue}
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("name1")
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;
                Assert.Equal(CustomValue, options.ConnectionString);
            }
        }

        [Fact]
        public void AddAzureOptionsFromConfiguration()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"Azure:SignalR:s1:ConnectionString", DefaultValue},
                        {"Azure:SignalR:s1:ServerStickyMode", "preferred" },
                        {"Azure:SignalR:s1:ApplicationName:", "ABC" },
                        {"Azure:SignalR:s1:InitialHubServerConnectionCount", "1" },
                        {"Azure:SignalR:s1:MaxHubServerConnectionCount", "2" },
                        {"Azure:SignalR:s1:AccessTokenLifetimeInSeconds", "1" },
                        {"Azure:SignalR:s1:ServiceScaleTimeoutInSeconds", "2" },
                        {"Azure:SignalR:s1:MaxPollIntervalInSeconds", "3" },
                        {"Azure:SignalR:s1:GracefulShutdown:Mode", "WaitForClientsClose" },
                        {"Azure:SignalR:s1:GracefulShutdown:TimeOutInSeconds", "4" },
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s1")
                    .Services
                    .AddSingleton<IConfiguration>(config)
                    .AddSingleton(loggerFactory)
                    .BuildServiceProvider();

                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

                Assert.Equal(DefaultValue, options.ConnectionString);
                Assert.Equal(ServerStickyMode.Preferred, options.ServerStickyMode);
                Assert.Equal("ABC", options.ApplicationName);
                Assert.Equal(1, options.InitialHubServerConnectionCount);
                Assert.Equal(2, options.MaxHubServerConnectionCount);
                Assert.Equal(1, options.AccessTokenLifetime.Seconds);
                Assert.Equal(2, options.ServiceScaleTimeout.Seconds);
                Assert.Equal(3, options.MaxPollIntervalInSeconds);
                Assert.Equal(GracefulShutdownMode.WaitForClientsClose, options.GracefulShutdown.Mode);
                Assert.Equal(4, options.GracefulShutdown.Timeout.Seconds);
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
                        {"Azure:SignalR:s2:ConnectionString", DefaultValue},
                        {"Azure:SignalR:s2:StickyServerMode", "invalid" }
                    })
                    .Build();
                string capturedConnectionString = null;
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s2", o => { capturedConnectionString = o.ConnectionString; })
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
        [InlineData(CustomValue, null, null, CustomValue)]
        [InlineData(CustomValue, DefaultValue, SecondaryValue, CustomValue)]
        [InlineData(null, DefaultValue, SecondaryValue, DefaultValue)]
        [InlineData(null, null, SecondaryValue, SecondaryValue)]
        public void AddNamedAzureSignalRLoadConnectionStringOrder(string customValue, string defaultValue,
            string secondaryValue, string expected)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:s1:ConnectionString", defaultValue},
                    {"ConnectionStrings:Azure:SignalR:s1:ConnectionString", secondaryValue}
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s1", o =>
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
        public void AddNamedAzureSignalRReadServiceEndpointsFromConfig(string customValue, string defaultValue,
            string secondaryValue, string expected)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:s1:ConnectionString", defaultValue},
                    {"Azure:SignalR:s1:ConnectionString:1:secondary", secondaryValue},
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s1", o =>
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
                var endpoints = serviceProvider.GetRequiredService<IServiceEndpointManager>().Endpoints.Keys.ToArray();
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
        public void AddNamedAzureSignalRWithOnlySecondaryValueThrows(string customValue, string defaultValue,
            string secondaryValue, string expected)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                    {"Azure:SignalR:s1:ConnectionString", defaultValue},
                    {"Azure:SignalR:s1:ConnectionString:1:secondary", secondaryValue},
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s1", o =>
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
        public void AddNamedAzureSignalRCustomizeEndpointsOverridesConfigValue(string defaultValue, string customValue, string expected, int expectedCount)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var services = new ServiceCollection();
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"Azure:SignalR:s1:ConnectionString", defaultValue},
                        {"Azure:SignalR:s1:ConnectionString:1:secondary", SecondaryValue},
                    })
                    .Build();
                var serviceProvider = services.AddSignalR()
                    .AddNamedAzureSignalR("s1", o =>
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

                var endpoints = endpointManager.Endpoints.Keys;

                Assert.Equal(expectedCount, endpoints.Count());

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