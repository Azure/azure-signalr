// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Owin;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class RunAzureSignalRTests : VerifiableLoggedTest
    {
        private static readonly Version VersionSupportingApplicationNamePrefix = new Version(1, 0, 9);

        private const string ServiceUrl = "http://localhost:8086";
        private const string ConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string ConnectionString2 = "Endpoint=http://localhost2;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string ConnectionString3 = "Endpoint=http://localhost3;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string ConnectionString4 = "Endpoint=http://localhost4;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string AppName = "AzureSignalRTest";

        public RunAzureSignalRTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void TestRunAzureSignalRWithDefaultOptions()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {

                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, ConnectionString, hubConfig)))
                {
                    var resolver = hubConfig.Resolver;
                    var options = resolver.Resolve<IOptions<ServiceOptions>>();
                    Assert.Equal(ConnectionString, options.Value.ConnectionString);
                    Assert.IsType<ServiceConnectionManager>(resolver.Resolve<IServiceConnectionManager>());
                    Assert.IsType<ServiceMessageBus>(resolver.Resolve<IMessageBus>());
                    Assert.IsType<AzureTransportManager>(resolver.Resolve<ITransportManager>());
                    Assert.IsType<ServiceProtocol>(resolver.Resolve<IServiceProtocol>());
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithAppNameEqualToHubNameThrows()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                var hubName = "hub";
                var testHub = new TestHubManager(hubName);
                hubConfig.Resolver.Register(typeof(IHubManager), () => testHub);
                var ex = Assert.Throws<ArgumentException>(() => WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(hubName, ConnectionString, hubConfig)));

                Assert.Equal("App name should not be the same as hub name.", ex.Message);
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithoutConnectionString()
        {
            var exception = Assert.Throws<ArgumentException>(
                    () =>
                    {
                        using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName)))
                        {
                        }
                    });
            Assert.StartsWith("No connection string was specified.", exception.Message);
        }

        [Fact]
        public void TestRunAzureSignalRWithInvalidConnectionString()
        {
            var exception = Assert.Throws<ArgumentException>(
                   () =>
                   {
                       using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, "A=b;c=d")))
                       {
                       }
                   });
            Assert.StartsWith("Connection string missing required properties endpoint and accesskey.", exception.Message);
        }

        [Fact]
        public void TestRunAzureSignalRWithConnectionString()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, ConnectionString, hubConfig)))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();
                    Assert.Equal(ConnectionString, options.Value.ConnectionString);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWiillUseApplicationNameInOptionsWhenUseHubPrefixIsTrue()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig, opts => { opts.ConnectionString = ConnectionString; })))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();
                    Assert.Equal(AppName, options.Value.ApplicationName);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithAppSettings()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig)))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString, options.Value.ConnectionString);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithMultipleAppSettings()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2, ConnectionString3, ConnectionString4))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig)))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString, options.Value.ConnectionString);

                    // split the default ConnectionString from Endpoints and merge it in EndpointManager
                    Assert.Equal(3, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Equal(4, endpoints.Length);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithSingleAppSettingsAndConfigureOptions()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig, s => s.ConnectionString = ConnectionString2)))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString2, options.Value.ConnectionString);

                    // The connection string configured in Configure should override the one defined in app setting
                    Assert.Empty(options.Value.Endpoints);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Single(endpoints);
                    Assert.Equal(ConnectionString2, endpoints[0].ConnectionString);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithMultipleAppSettingsAndConnectionStringConfigured()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2, ConnectionString3, ConnectionString4))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl,
                    app => app.RunAzureSignalR(AppName, hubConfig, s => s.ConnectionString = ConnectionString4)))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString4, options.Value.ConnectionString);

                    Assert.Equal(3, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Equal(3, endpoints.Length);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithMultipleAppSettingsAndEndpointsConfigured()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2, ConnectionString3, ConnectionString4))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl,
                    app => app.RunAzureSignalR(AppName, hubConfig, s => s.Endpoints = new[]
                    {
                        new ServiceEndpoint(ConnectionString2, EndpointType.Secondary),
                        new ServiceEndpoint(ConnectionString3),
                        new ServiceEndpoint(ConnectionString4)
                    })))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString, options.Value.ConnectionString);

                    Assert.Equal(3, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Equal(4, endpoints.Length);
                }
            }
        }

        [Theory]
        [InlineData(null, 3)]
        [InlineData("", 3)]
        [InlineData(ConnectionString2, 3)]
        [InlineData(ConnectionString, 4)]
        public void TestRunAzureSignalRWithMultipleAppSettingsAndBothConnectionStringAndEndpointsCustomized(string customConnectionString, int expectedCount)
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2, ConnectionString3, ConnectionString4))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl,
                    app => app.RunAzureSignalR(AppName, hubConfig, s =>
                    {
                        s.ConnectionString = customConnectionString;
                        s.Endpoints = new[]
                        {
                            new ServiceEndpoint(ConnectionString2, EndpointType.Secondary),
                            new ServiceEndpoint(ConnectionString3),
                            new ServiceEndpoint(ConnectionString4)
                        };
                    })))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(customConnectionString, options.Value.ConnectionString);

                    Assert.Equal(3, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Equal(expectedCount, endpoints.Length);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithMultipleAppSettingsAndBothConnectionStringAndEndpointsConfigured()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2, ConnectionString3, ConnectionString4))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl,
                    app => app.RunAzureSignalR(AppName, hubConfig, 
                        s =>
                        {
                            s.ConnectionString = ConnectionString;
                            s.Endpoints = new[]
                            {
                                new ServiceEndpoint(ConnectionString2, EndpointType.Secondary),
                                new ServiceEndpoint(ConnectionString3),
                                new ServiceEndpoint(ConnectionString4)
                            };
                        })))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString, options.Value.ConnectionString);

                    Assert.Equal(3, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Equal(4, endpoints.Length);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithMultipleAppSettingsAndCustomSettings()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig, options =>
                {
                    options.Endpoints = new ServiceEndpoint[]
                    {
                        new ServiceEndpoint(ConnectionString2, EndpointType.Secondary),
                        new ServiceEndpoint(ConnectionString3),
                        new ServiceEndpoint(ConnectionString4)
                    };
                })))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString, options.Value.ConnectionString);

                    Assert.Equal(3, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Equal(4, endpoints.Length);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithMultipleAppSettingsAndCustomSettingsIncludingOptionsAppName()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig, options =>
                {
                    options.Endpoints = new ServiceEndpoint[]
                    {
                        new ServiceEndpoint(ConnectionString2, EndpointType.Secondary),
                        new ServiceEndpoint(ConnectionString3),
                        new ServiceEndpoint(ConnectionString4)
                    };
                })))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString, options.Value.ConnectionString);
                    Assert.Equal(AppName, options.Value.ApplicationName);

                    Assert.Equal(3, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Equal(4, endpoints.Length);
                }
            }
        }

        [Fact]
        public async Task TestRunAzureSignalRWithMultipleAppSettingsAndCustomSettingsAndCustomRouter()
        {
            // Prepare the configuration
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                var router = new TestEndpointRouter();
                hubConfig.Resolver.Register(typeof(IEndpointRouter), () => router);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig, options =>
                {
                    options.Endpoints = new ServiceEndpoint[]
                    {
                        new ServiceEndpoint(ConnectionString2, EndpointType.Secondary),
                        new ServiceEndpoint(ConnectionString3, name: "chosen"),
                        new ServiceEndpoint(ConnectionString4)
                    };
                })))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString, options.Value.ConnectionString);

                    Assert.Equal(3, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.Endpoints;
                    Assert.Equal(4, endpoints.Length);

                    var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                    var response = await client.GetAsync("/negotiate?endpoint=chosen");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    var message = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<ResponseMessage>(message);
                    Assert.Equal("2.0", responseObject.ProtocolVersion);

                    // with custome router, always goes to connection string 3 as passed into the router
                    Assert.Equal("http://localhost3/aspnetclient", responseObject.RedirectUrl);

                    // Invalid request
                    response = await client.GetAsync("/negotiate");

                    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

                    // Invalid request
                    response = await client.GetAsync("/negotiate?endpoint=notexists");

                    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
                }
            }
        }

        [Fact]
        public async Task TestRunAzureSignalRWithDefaultRouterNegotiateWithFallback()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                // Prepare the configuration
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);

                hubConfig.Resolver.Register(typeof(ILoggerFactory), () => loggerFactory);
                var router = new DefaultEndpointRouter();
                hubConfig.Resolver.Register(typeof(IEndpointRouter), () => router);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig, options =>
                {
                    options.Endpoints = new ServiceEndpoint[]
                    {
                        new ServiceEndpoint(ConnectionString2, EndpointType.Secondary),
                        new ServiceEndpoint(ConnectionString3)
                        {
                            Connection = new TestServiceConnectionContainer(ServiceConnectionStatus.Disconnected)
                        },
                        new ServiceEndpoint(ConnectionString4)
                        {
                            Connection = new TestServiceConnectionContainer(ServiceConnectionStatus.Disconnected)
                        },
                    };
                })))
                {
                    var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                    var response = await client.GetAsync("/negotiate");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    var message = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<ResponseMessage>(message);
                    Assert.Equal("2.0", responseObject.ProtocolVersion);

                    // The default router fallbacks to the secondary
                    Assert.Equal("http://localhost2/aspnetclient", responseObject.RedirectUrl);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithOptions()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var hubConfig = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig, o =>
                {
                    o.ConnectionString = ConnectionString;
                    o.ConnectionCount = -1;
                })))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();
                    Assert.Equal(ConnectionString, options.Value.ConnectionString);
                    Assert.Equal(-1, options.Value.ConnectionCount);
                }
            }
        }

        [Theory]
        [InlineData(typeof(NullUserIdProvider), null)]
        [InlineData(typeof(CustomUserIdProvider), "hello")]
        public async Task TestRequestsWithRunAzureSignalR(Type providerType, string expectedUser)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var hubConfiguration = Utility.GetTestHubConfig(loggerFactory);
                hubConfiguration.Resolver.Register(typeof(IUserIdProvider), () => Activator.CreateInstance(providerType));
                using (WebApp.Start(ServiceUrl, a => a.RunAzureSignalR(AppName, ConnectionString, hubConfiguration)))
                {
                    var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                    var response = await client.GetAsync("/negotiate");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    var message = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<ResponseMessage>(message);
                    Assert.Equal("2.0", responseObject.ProtocolVersion);
                    Assert.Equal("http://localhost/aspnetclient", responseObject.RedirectUrl);
                    Assert.NotNull(responseObject.AccessToken);
                    var token = JwtSecurityTokenHandler.ReadJwtToken(responseObject.AccessToken);
                    Assert.Equal(AppName, token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.AppName).Value);
                    var user = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.UserId)?.Value;
                    Assert.Equal(expectedUser, user);

                    // 1. test client proxy file can return
                    response = await client.GetAsync("/signalr/hubs");
                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    message = await response.Content.ReadAsStringAsync();
                    Assert.StartsWith("/*!\r\n * ASP.NET SignalR JavaScript ", message);

                    // 2. test other requests should not be handled
                    response = await client.GetAsync("/not-exists");
                    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                }
            }
        }

        [Fact]
        public async Task TestClaimsProviderInServiceOptionsTakeEffect()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var hubConfiguration = Utility.GetTestHubConfig(loggerFactory);
                using (WebApp.Start(ServiceUrl, a => a.RunAzureSignalR(AppName, hubConfiguration, options =>
                {
                    options.ConnectionString = ConnectionString;
                    options.ClaimsProvider = context => new Claim[]
                    {
                    new Claim("user", "hello"),
                    };
                    options.AccessTokenLifetime = TimeSpan.FromDays(1);
                })))
                {
                    var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                    var response = await client.GetAsync("/negotiate");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    var message = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<ResponseMessage>(message);
                    Assert.Equal("2.0", responseObject.ProtocolVersion);
                    Assert.Equal("http://localhost/aspnetclient", responseObject.RedirectUrl);
                    Assert.NotNull(responseObject.AccessToken);
                    var token = JwtSecurityTokenHandler.ReadJwtToken(responseObject.AccessToken);
                    Assert.Equal(AppName, token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.AppName).Value);
                    var user = token.Claims.FirstOrDefault(s => s.Type == "user")?.Value;
                    Assert.Equal("hello", user);
                    Assert.Null(token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerName));
                    Assert.Null(token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerStickyMode));

                    var version = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.Version)?.Value;
                    Assert.True(Version.TryParse(version, out var vs));
                    Assert.True(vs >= VersionSupportingApplicationNamePrefix);
                    var requestId = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.Id);
                    Assert.NotNull(requestId);
                    Assert.Equal(TimeSpan.FromDays(1), token.ValidTo - token.ValidFrom);
                }
            }
        }

        [Fact]
        public async Task TestStickyServerInServiceOptionsTakeEffect()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var name = nameof(TestStickyServerInServiceOptionsTakeEffect);
                var hubConfiguration = Utility.GetTestHubConfig(loggerFactory);
                var serverNameProvider = new TestServerNameProvider(name);
                hubConfiguration.Resolver.Register(typeof(IServerNameProvider), () => serverNameProvider);
                using (WebApp.Start(ServiceUrl, a => a.RunAzureSignalR(AppName, hubConfiguration, options =>
                {
                    options.ServerStickyMode = ServerStickyMode.Required;
                    options.ConnectionString = ConnectionString;
                    options.ClaimsProvider = context => new Claim[]
                    {
                    new Claim("user", "hello"),
                    };
                    options.AccessTokenLifetime = TimeSpan.FromDays(1);
                })))
                {
                    var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                    var response = await client.GetAsync("/negotiate");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    var message = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<ResponseMessage>(message);
                    Assert.Equal("2.0", responseObject.ProtocolVersion);
                    Assert.Equal("http://localhost/aspnetclient", responseObject.RedirectUrl);
                    Assert.NotNull(responseObject.AccessToken);
                    var token = JwtSecurityTokenHandler.ReadJwtToken(responseObject.AccessToken);
                    Assert.Equal(AppName, token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.AppName).Value);
                    var user = token.Claims.FirstOrDefault(s => s.Type == "user")?.Value;
                    Assert.Equal("hello", user);
                    var serverName = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerName)?.Value;
                    Assert.Equal(name, serverName);
                    var mode = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerStickyMode)?.Value;
                    Assert.Equal("Required", mode);
                    Assert.NotNull(token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.ServerStickyMode));
                    var version = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.Version)?.Value;
                    Version.TryParse(version, out var versionResult);
                    Assert.True(versionResult > new Version(1, 0, 8));

                    var requestId = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.Id);
                    Assert.NotNull(requestId);
                    Assert.Equal(TimeSpan.FromDays(1), token.ValidTo - token.ValidFrom);
                }
            }
        }

        [Fact]
        public async Task TestRunAzureSignalRWithAnonymousUserOnAuthrizedHubReturnFail()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var hubConfig = new HubConfiguration();
                hubConfig.Resolver = new DefaultDependencyResolver();
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, ConnectionString, hubConfig)))
                {
                    var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                    var response = await client.GetAsync("/negotiate?connectionData=%5B%7B%22name%22%3A%22authchat%22%7D%5D");
                    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                }
            }
        }

        private sealed class TestServerNameProvider : IServerNameProvider
        {
            private readonly string _serverName;
            public TestServerNameProvider(string serverName)
            {
                _serverName = serverName;
            }

            public string GetName()
            {
                return _serverName;
            }
        }

        private sealed class AppSettingsConfigScope : IDisposable
        {
            private readonly string _originalSetting;

            private readonly List<KeyValuePair<string, string>> _originalAdditonalSettings;

            public AppSettingsConfigScope(string setting, params string[] additionalSettings)
            {
                _originalSetting = ConfigurationManager.AppSettings[Constants.ConnectionStringDefaultKey];
                ConfigurationManager.AppSettings[Constants.ConnectionStringDefaultKey] = setting;

                var newSettings = additionalSettings.Select(
                    s =>
                    new KeyValuePair<string, string>(
                        Constants.ConnectionStringKeyPrefix + Guid.NewGuid().ToString("N")
                        , s))
                    .ToList();
                _originalAdditonalSettings = newSettings.Select(s =>
                {
                    var original = ConfigurationManager.AppSettings[s.Key];
                    ConfigurationManager.AppSettings[s.Key] = s.Value;
                    return new KeyValuePair<string, string>(s.Key, original);
                }).ToList();

            }

            public void Dispose()
            {
                ConfigurationManager.AppSettings[Constants.ConnectionStringDefaultKey] = _originalSetting;
                foreach (var pair in _originalAdditonalSettings)
                {
                    ConfigurationManager.AppSettings[pair.Key] = pair.Value;
                }
            }
        }

        private sealed class NullUserIdProvider : IUserIdProvider
        {
            public string GetUserId(IRequest request)
            {
                return null;
            }
        }

        private sealed class CustomUserIdProvider : IUserIdProvider
        {
            public string GetUserId(IRequest request)
            {
                return "hello";
            }
        }

        private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new JwtSecurityTokenHandler();

        private sealed class ResponseMessage
        {
            public string ProtocolVersion { get; set; }

            public string RedirectUrl { get; set; }

            public string AccessToken { get; set; }
        }
    }
}