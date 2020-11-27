// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceManagerFacts
    {
        private const string Endpoint = "https://abc";
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private const string HubName = "signalrBench";
        private const string UserId = "UserA";
        private const string UserAgent = "userAgent";
        private static readonly string _testConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";
        private static readonly TimeSpan _tokenLifeTime = TimeSpan.FromSeconds(99);
        private static readonly Claim[] _defaultClaims = new Claim[] { new Claim("type1", "val1") };
        private static readonly ServiceTransportType[] _serviceTransportTypes = new ServiceTransportType[] { ServiceTransportType.Transient, ServiceTransportType.Persistent };
        private static readonly bool[] _useLoggerFatories = new bool[] { false, true };
        private static readonly string[] _appNames = new string[] { "appName", "", null };
        private static readonly string[] _userIds = new string[] { UserId, null };
        private static readonly IEnumerable<Claim[]> _claimLists = new Claim[][] { _defaultClaims, null };
        private static readonly int[] _connectionCounts = new int[] { 1, 2 };

        public static IEnumerable<object[]> TestServiceManagerOptionData => from transport in _serviceTransportTypes
                                                                            from useLoggerFactory in _useLoggerFatories
                                                                            from appName in _appNames
                                                                            from connectionCount in _connectionCounts
                                                                            select new object[] { transport, useLoggerFactory, appName, connectionCount };

        public static IEnumerable<object[]> TestGenerateClientEndpointData => from appName in _appNames
                                                                              select new object[] { appName, GetExpectedClientEndpoint(appName) };

        public static IEnumerable<object[]> TestGenerateAccessTokenData => from userId in _userIds
                                                                           from claims in _claimLists
                                                                           from appName in _appNames
                                                                           select new object[] { userId, claims, appName };

        [Theory]
        [MemberData(nameof(TestGenerateAccessTokenData))]
        internal void GenerateClientAccessTokenTest(string userId, Claim[] claims, string appName)
        {
            var builder = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ApplicationName = appName;
                    o.ConnectionString = _testConnectionString;
                });
            var manager = builder.Build();
            var tokenString = manager.GenerateClientAccessToken(HubName, userId, claims, _tokenLifeTime);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            string expectedToken = JwtTokenHelper.GenerateExpectedAccessToken(token, GetExpectedClientEndpoint(appName), AccessKey, claims);

            Assert.Equal(expectedToken, tokenString);
        }

        [Theory]
        [MemberData(nameof(TestGenerateClientEndpointData))]
        internal void GenerateClientEndpointTest(string appName, string expectedClientEndpoint)
        {
            var builder = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ApplicationName = appName;
                    o.ConnectionString = _testConnectionString;
                });
            var manager = builder.Build();
            var clientEndpoint = manager.GetClientEndpoint(HubName);

            Assert.Equal(expectedClientEndpoint, clientEndpoint);
        }

        [Fact]
        internal void GenerateClientEndpointTestWithClientEndpoint()
        {
            var manager = new ServiceManagerBuilder().WithOptions(o =>
            {
                o.Endpoints = new ServiceEndpoint[] { new ServiceEndpoint($"Endpoint=http://localhost;AccessKey=ABC;Version=1.0;ClientEndpoint=https://remote") };
            }).Build();
            var clientEndpoint = manager.GetClientEndpoint(HubName);

            Assert.Equal("https://remote/client/?hub=signalrbench", clientEndpoint);
        }

        [Theory(Skip = "Reenable when it is ready")]
        [MemberData(nameof(TestServiceManagerOptionData))]
        internal async Task CreateServiceHubContextTest(ServiceTransportType serviceTransportType, bool useLoggerFacory, string appName, int connectionCount)
        {
            var builder = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = serviceTransportType;
                    o.ApplicationName = appName;
                    o.ConnectionCount = connectionCount;
                    o.ConnectionString = _testConnectionString;
                });
            var serviceManager = builder.Build();

            using (var loggerFactory = useLoggerFacory ? (ILoggerFactory)new LoggerFactory() : NullLoggerFactory.Instance)
            {
                var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
            }
        }

        [Fact]
        internal async Task IsServiceHealthy_ReturnTrue_Test()
        {
            var services = new ServiceCollection();
            services.AddSignalRServiceManager();
            services.Configure<ServiceManagerOptions>(o => o.ConnectionString = _testConnectionString);
            services.AddSingleton<RestClientFactory>(new TestRestClientFactory(UserAgent, HttpStatusCode.OK));
            using var serviceProvider = services.BuildServiceProvider();
            var serviceManager = serviceProvider.GetRequiredService<IServiceManager>();
            var actual = await serviceManager.IsServiceHealthy(default);

            Assert.True(actual);
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        internal async Task IsServiceHealthy_ReturnFalse_Test(HttpStatusCode statusCode)
        {
            var services = new ServiceCollection();
            services.Configure<ServiceManagerOptions>(o => o.ConnectionString = _testConnectionString);
            services.AddSignalRServiceManager();
            services.AddSingleton<RestClientFactory>(new TestRestClientFactory(UserAgent, statusCode));
            using var serviceProvider = services.BuildServiceProvider();
            var serviceManager = serviceProvider.GetRequiredService<IServiceManager>();

            var actual = await serviceManager.IsServiceHealthy(default);

            Assert.False(actual);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, typeof(AzureSignalRInvalidArgumentException))]
        [InlineData(HttpStatusCode.Unauthorized, typeof(AzureSignalRUnauthorizedException))]
        [InlineData(HttpStatusCode.NotFound, typeof(AzureSignalRInaccessibleEndpointException))]
        [InlineData(HttpStatusCode.Ambiguous, typeof(AzureSignalRRuntimeException))]
        internal async Task IsServiceHealthy_Throw_Test(HttpStatusCode statusCode, Type expectedException)
        {
            var services = new ServiceCollection();
            services.AddSignalRServiceManager();
            services.Configure<ServiceManagerOptions>(o => o.ConnectionString = _testConnectionString);
            services.AddSingleton<RestClientFactory>(new TestRestClientFactory(UserAgent, statusCode));
            using var serviceProvider = services.BuildServiceProvider();
            var serviceManager = serviceProvider.GetRequiredService<IServiceManager>();

            var exception = await Assert.ThrowsAnyAsync<AzureSignalRException>(() => serviceManager.IsServiceHealthy(default));
            Assert.IsType(expectedException, exception);
        }

        [Fact]
        public void DisposeTest()
        {
            var serviceManager = new ServiceManagerBuilder().WithOptions(o => o.ConnectionString = _testConnectionString).Build();
            serviceManager.Dispose();
        }

        private static string GetExpectedClientEndpoint(string appName = null)
        {
            if (string.IsNullOrEmpty(appName))
            {
                return $"{Endpoint}/client/?hub={HubName.ToLower()}";
            }

            return $"{Endpoint}/client/?hub={appName.ToLower()}_{HubName.ToLower()}";
        }
    }
}