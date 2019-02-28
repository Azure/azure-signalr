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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Owin;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class RunAzureSignalRTests
    {
        private const string ServiceUrl = "http://localhost:8086";
        private const string ConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string ConnectionString2 = "Endpoint=http://localhost2;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string ConnectionString3 = "Endpoint=http://localhost3;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string ConnectionString4 = "Endpoint=http://localhost4;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";
        private const string AppName = "AzureSignalRTest";

        [Fact]
        public void TestRunAzureSignalRWithDefaultOptions()
        {
            var hubConfig = GetEmptyHubConfig();
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

        [Fact]
        public void TestRunAzureSignalRWithAppNameEqualToHubNameThrows()
        {
            var hubConfig = GetEmptyHubConfig();
            var hubName = "hub";
            var testHub = new TestHubManager(hubName);
            hubConfig.Resolver.Register(typeof(IHubManager), () => testHub);
            var ex = Assert.Throws<ArgumentException>(() => WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(hubName, ConnectionString, hubConfig)));

            Assert.Equal("App name should not be the same as hub name.", ex.Message);
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
            var hubConfig = GetEmptyHubConfig();
            using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, ConnectionString, hubConfig)))
            {
                var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();
                Assert.Equal(ConnectionString, options.Value.ConnectionString);
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithAppSettings()
        {
            // Prepare the configuration
            using (new AppSettingsConfigScope(ConnectionString))
            {
                var hubConfig = GetEmptyHubConfig();
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
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2, ConnectionString3, ConnectionString4))
            {
                var hubConfig = GetEmptyHubConfig();
                using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig)))
                {
                    var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();

                    Assert.Equal(ConnectionString, options.Value.ConnectionString);

                    Assert.Equal(4, options.Value.Endpoints.Length);

                    var manager = hubConfig.Resolver.Resolve<IServiceEndpointManager>();
                    var endpoints = manager.GetAvailableEndpoints().ToArray();
                    Assert.Equal(4, endpoints.Length);
                }
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithMultipleAppSettingsAndCustomSettings()
        {
            // Prepare the configuration
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2))
            {
                var hubConfig = GetEmptyHubConfig();
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
                    var endpoints = manager.GetAvailableEndpoints().ToArray();
                    Assert.Equal(4, endpoints.Length);
                }
            }
        }

        [Fact]
        public async Task TestRunAzureSignalRWithMultipleAppSettingsAndCustomSettingsAndCustomRouter()
        {
            // Prepare the configuration
            using (new AppSettingsConfigScope(ConnectionString, ConnectionString2))
            {
                var hubConfig = GetEmptyHubConfig();
                var router = new TestCustomRouter(ConnectionString3);
                hubConfig.Resolver.Register(typeof(IEndpointRouter), () => router);
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
                    var endpoints = manager.GetAvailableEndpoints().ToArray();
                    Assert.Equal(4, endpoints.Length);

                    var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                    var response = await client.GetAsync("/negotiate");

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    var message = await response.Content.ReadAsStringAsync();
                    var responseObject = JsonConvert.DeserializeObject<ResponseMessage>(message);
                    Assert.Equal("2.0", responseObject.ProtocolVersion);

                    // with custome router, always goes to connection string 3 as passed into the router
                    Assert.Equal("http://localhost3/aspnetclient", responseObject.RedirectUrl);
                }
            }
        }

        [Fact]
        public async Task TestRunAzureSignalRWithDefaultRouterNegotiateWithFallback()
        {
            // Prepare the configuration
            var hubConfig = new HubConfiguration();
            hubConfig.Resolver = new DefaultDependencyResolver();
            var router = new DefaultEndpointRouter();
            hubConfig.Resolver.Register(typeof(IEndpointRouter), () => router);
            using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(AppName, hubConfig, options =>
            {
                options.Endpoints = new ServiceEndpoint[]
                {
                        new ServiceEndpoint(ConnectionString2, EndpointType.Secondary),
                        new ServiceEndpoint(ConnectionString3)
                        {
                            Connection = new TestContainer(ServiceConnectionStatus.Disconnected)
                        },
                        new ServiceEndpoint(ConnectionString4)
                        {
                            Connection = new TestContainer(ServiceConnectionStatus.Disconnected)
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

        [Fact]
        public void TestRunAzureSignalRWithOptions()
        {
            var hubConfig = GetEmptyHubConfig();
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

        [Theory]
        [InlineData(typeof(NullUserIdProvider), null)]
        [InlineData(typeof(CustomUserIdProvider), "hello")]
        public async Task TestRequestsWithRunAzureSignalR(Type providerType, string expectedUser)
        {
            var hubConfiguration = GetEmptyHubConfig();
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

        [Fact]
        public async Task TestClaimsProviderInServiceOptionsTakeEffect()
        {
            var hubConfiguration = GetEmptyHubConfig();
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
                var requestId = token.Claims.FirstOrDefault(s => s.Type == Constants.ClaimType.Id);
                Assert.NotNull(requestId);
                Assert.Equal(TimeSpan.FromDays(1), token.ValidTo - token.ValidFrom);
            }
        }

        private static HubConfiguration GetEmptyHubConfig()
        {
            var hubConfig = new HubConfiguration();
            // Resolver is shared in GloblHost, use a new one instead
            hubConfig.Resolver = new DefaultDependencyResolver();
            var testHub = new TestHubManager();
            hubConfig.Resolver.Register(typeof(IHubManager), () => testHub);
            return hubConfig;
        }

        private class TestContainer : IServiceConnectionContainer
        {
            public ServiceConnectionStatus Status { get; }

            public TestContainer(ServiceConnectionStatus status)
            {
                Status = status;
            }
            public Task StartAsync()
            {
                throw new NotImplementedException();
            }

            public Task WriteAsync(ServiceMessage serviceMessage)
            {
                throw new NotImplementedException();
            }

            public Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private class TestCustomRouter : IEndpointRouter
        {
            private readonly string _negotiateEndpoint;

            public TestCustomRouter(string negotiateEndpoint)
            {
                _negotiateEndpoint = negotiateEndpoint;
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                return availableEnpoints;
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                return availableEnpoints;
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                return availableEnpoints;
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForGroups(IReadOnlyList<string> groupList, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                return availableEnpoints;
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                return availableEnpoints;
            }

            public IEnumerable<ServiceEndpoint> GetEndpointsForUsers(IReadOnlyList<string> userList, IEnumerable<ServiceEndpoint> availableEnpoints)
            {
                return availableEnpoints;
            }

            public ServiceEndpoint GetNegotiateEndpoint(IEnumerable<ServiceEndpoint> primaryEndpoints)
            {
                return primaryEndpoints.First(e => e.ConnectionString == _negotiateEndpoint);
            }
        }

        private sealed class TestHubManager : IHubManager
        {
            private readonly string[] _hubs;
            public TestHubManager(params string[] hubs)
            {
                _hubs = hubs;
            }

            public HubDescriptor GetHub(string hubName)
            {
                return null;
            }

            public MethodDescriptor GetHubMethod(string hubName, string method, IList<IJsonValue> parameters)
            {
                return null;
            }

            public IEnumerable<MethodDescriptor> GetHubMethods(string hubName, Func<MethodDescriptor, bool> predicate)
            {
                yield break;
            }

            public IEnumerable<HubDescriptor> GetHubs(Func<HubDescriptor, bool> predicate)
            {
                return _hubs.Select(s => new HubDescriptor() { Name = s });
            }

            public IHub ResolveHub(string hubName)
            {
                return null;
            }

            public IEnumerable<IHub> ResolveHubs()
            {
                yield break;
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