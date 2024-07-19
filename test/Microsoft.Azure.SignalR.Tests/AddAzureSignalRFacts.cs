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

namespace Microsoft.Azure.SignalR.Tests;

public class AddAzureSignalRFacts : VerifiableLoggedTest
{
    private const string CustomValue = "Endpoint=https://customconnectionstring;AccessKey=1";
    private const string DefaultValue = "Endpoint=https://defaultconnectionstring;AccessKey=1";
    private const string SecondaryValue = "Endpoint=https://secondaryconnectionstring;AccessKey=1";
    private const string ConfigFile = "testappsettings.json";

    public AddAzureSignalRFacts(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    [Obsolete]
    public void AddAzureSignalRReadsDefaultConfigurationKeyForConnectionString()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                {"Azure:SignalR:ConnectionString", DefaultValue},
                {"Azure:SignalR:ApplicationName", "Application1"}
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
            Assert.Equal(5, options.ConnectionCount);
            Assert.Equal("Application1", options.ApplicationName);
            Assert.Equal(5, options.InitialHubServerConnectionCount);
            Assert.Equal(TimeSpan.FromHours(1), options.AccessTokenLifetime);
            Assert.Null(options.ClaimsProvider);
        }
    }

    [Fact]
    public void AddAzureSignalRReadsInvalidCongifurationThrows()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR( o =>
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
    [Obsolete]
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
            Assert.Equal(1, options.InitialHubServerConnectionCount);
            Assert.Equal(TimeSpan.FromHours(1), options.AccessTokenLifetime);
            Assert.Null(options.ClaimsProvider);
            Assert.Null(options.DiagnosticClientFilter);
        }
    }

    [Fact]
    public void AddAzureWithDiagnosticClientRuleProvider()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            Func<HttpContext, bool> diagnosticClientFilter = context => context.Request.Query["diag"][0] != null;
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR(o =>
                {
                    o.DiagnosticClientFilter = diagnosticClientFilter;
                })
                .Services
                .AddSingleton<IConfiguration>(config)
                .AddSingleton(loggerFactory)
                .BuildServiceProvider();

            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;

            Assert.Equal(diagnosticClientFilter, options.DiagnosticClientFilter);
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
                    {"Azure:SignalR:ConnectionString", DefaultValue},
                    {"Azure:SignalR:ServerStickyMode", "preferred" },
                    {"Azure:SignalR:ApplicationName:", "ABC" },
                    {"Azure:SignalR:InitialHubServerConnectionCount", "1" },
                    {"Azure:SignalR:MaxHubServerConnectionCount", "2" },
                    {"Azure:SignalR:AccessTokenLifetimeInSeconds", "1" },
                    {"Azure:SignalR:ServiceScaleTimeoutInSeconds", "2" },
                    {"Azure:SignalR:MaxPollIntervalInSeconds", "3" },
                    {"Azure:SignalR:GracefulShutdown:Mode", "WaitForClientsClose" },
                    {"Azure:SignalR:GracefulShutdown:TimeOutInSeconds", "4" },
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
    [InlineData("preferred", ServerStickyMode.Preferred)]
    [InlineData("Preferred", ServerStickyMode.Preferred)]
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

    [Theory]
    [InlineData("")]
    [InlineData("c_0")]
    [InlineData("C_0")]
    public void AddAzureSignalRWithValidAppName(string appName)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            ServiceProvider serviceProvider = null;
            var config = new ConfigurationBuilder().Build();
            serviceProvider = new ServiceCollection().AddSignalR()
                        .AddAzureSignalR(o =>
                        {
                            o.ApplicationName = appName;
                        }).Services
                        .AddSingleton(loggerFactory)
                        .AddSingleton<IConfiguration>(config)
                        .BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;
            Assert.Equal(appName, options.ApplicationName);
        }
    }

    [Theory]
    [InlineData("0c")]
    [InlineData("_c")]
    [InlineData("c-d")]
    public void AddAzureSignalRWithInValidAppName(string appName)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var propertyName = "ApplicationName";
            var validScope = "prefixed with alphabetic characters and only contain alpha-numeric characters or underscore";
            ServiceProvider serviceProvider = null;
            var config = new ConfigurationBuilder().Build();
            serviceProvider = new ServiceCollection().AddSignalR()
                        .AddAzureSignalR(o =>
                        {
                            o.ApplicationName = appName;
                        }).Services
                        .AddSingleton(loggerFactory)
                        .AddSingleton<IConfiguration>(config)
                        .BuildServiceProvider();
            var e = Assert.Throws<AzureSignalRInvalidServiceOptionsException>(() =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ServiceOptions>>().Value;
            });
            Assert.Equal($"Property '{propertyName}' value should be {validScope}.", e.Message);
        }
    }


    [Fact(Skip = "Manual run for CI stable")]
    public async Task AddAzureSignalRHotReloadConfigValue()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var tcs = new TaskCompletionSource<object>();
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddJsonFile(ConfigFile, optional: false, reloadOnChange: true)
                .Build();
            var serviceProvider = services.AddSignalR()
                .AddAzureSignalR("Endpoint=https://test1connectionstring;AccessKey=1")
                .Services
                .AddSingleton<IConfiguration>(config)
                .AddSingleton(loggerFactory)
                .BuildServiceProvider();

            var optionsMonitor = serviceProvider.GetService<IOptionsMonitor<ServiceOptions>>();
            var options = optionsMonitor.CurrentValue;

            var manager = serviceProvider.GetService<IServiceEndpointManager>();

            // All EPs including code and config with total 3
            Assert.Equal(3, manager.Endpoints.Count);

            // Update config file to add a new endpoint ConnectionString	
            var customeCS = "Endpoint=https://customconnectionstring;AccessKey=1";
            var text = File.ReadAllText(ConfigFile);
            var jsonObj = JsonConvert.DeserializeObject<JObject>(text);
            var endpoints = (JArray)jsonObj["Azure"]["SignalR"]["ConnectionString"];
            var newEndpoint = new JObject()
            {
                { "EP3:Primary", customeCS }
            };

            endpoints.Add(newEndpoint);
            var output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
            File.WriteAllText(ConfigFile, output);

            // give a few delay for change detected	
            await Task.Delay(1000);

            // Reload includes all endpoints
            Assert.Equal(4, manager.Endpoints.Count);
            Assert.Single(manager.Endpoints.Where(x => x.Value.ConnectionString == customeCS));
        }
    }

    [Fact]
    public async Task AddAzureSignalRWithCustomHandshakeTimeout()
    {
        // set custom handshake timeout in global hub options
        var claims = await GetClaims(sc => sc.AddSignalR(o => o.HandshakeTimeout = TimeSpan.FromSeconds(1)).AddAzureSignalR());
        Assert.Contains(claims, c => c.Type == Constants.ClaimType.CustomHandshakeTimeout && c.Value == "1");

        // set custom handshake timeout in particular hub options to override the settings in global hub options
        claims = await GetClaims(sc => sc.AddSignalR(o => o.HandshakeTimeout = TimeSpan.FromSeconds(1)).AddHubOptions<TestHub>(o => o.HandshakeTimeout = TimeSpan.FromSeconds(2)).AddAzureSignalR());
        Assert.Contains(claims, c => c.Type == Constants.ClaimType.CustomHandshakeTimeout && c.Value == "2");

        // no custom timeout
        claims = await GetClaims(sc => sc.AddSignalR().AddAzureSignalR());
        Assert.DoesNotContain(claims, c => c.Type == Constants.ClaimType.CustomHandshakeTimeout);

        // invalid timeout: larger than 30s
        claims = await GetClaims(sc => sc.AddSignalR(o => o.HandshakeTimeout = TimeSpan.FromSeconds(31)).AddAzureSignalR());
        Assert.DoesNotContain(claims, c => c.Type == Constants.ClaimType.CustomHandshakeTimeout);

        // invalid timeout: smaller than 1s
        claims = await GetClaims(sc => sc.AddSignalR(o => o.HandshakeTimeout = TimeSpan.FromSeconds(0)).AddAzureSignalR());
        Assert.DoesNotContain(claims, c => c.Type == Constants.ClaimType.CustomHandshakeTimeout);
    }

    private static async Task<IEnumerable<Claim>> GetClaims(Action<ServiceCollection> addSignalR)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                {"Azure:SignalR:ConnectionString", "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQR55555555012345678933333333;Version=1.0;"}
            })
            .Build();

        var services = new ServiceCollection();
        addSignalR(services);

        var sp = services
            .AddLogging()
            .AddSingleton<IHostApplicationLifetime>(new EmptyApplicationLifetime())
            .AddSingleton<IConfiguration>(config)
            .BuildServiceProvider();

        var app = new ApplicationBuilder(sp);
        app.UseRouting();
        app.UseEndpoints(routes =>
        {
            routes.MapHub<TestHub>("/chat");
        });

        var h = sp.GetRequiredService<NegotiateHandler<TestHub>>();
        var r = await h.Process(new DefaultHttpContext());
        var jwtSecurityTokenHandler = new JwtSecurityTokenHandler();
        var t = jwtSecurityTokenHandler.ReadJwtToken(r.AccessToken);
        return t.Claims;
    }
}