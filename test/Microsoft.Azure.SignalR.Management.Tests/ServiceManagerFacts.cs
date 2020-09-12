// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public interface IServiceHubTestClient
    {
        Task HelloWorld();
    }

    public class ServiceManagerFacts
    {
        private const string Endpoint = "https://abc";
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private const string HubName = "signalrBench";
        private const string UserId = "UserA";
        private static readonly string _testConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";
        private static readonly TimeSpan _tokenLifeTime = TimeSpan.FromSeconds(99);
        private static readonly Claim[] _defaultClaims = new Claim[] { new Claim("type1", "val1") };
        private static readonly ServiceTransportType[] _serviceTransportTypes = new ServiceTransportType[] { ServiceTransportType.Transient, ServiceTransportType.Persistent };
        private static readonly bool[] _useLoggerFatories = new bool[] { false, true };
        private static readonly string[] _appNames = new string[] { "appName", "", null };
        private static readonly string[] _userIds = new string[] { UserId, null };
        private static readonly IEnumerable<Claim[]> _claimLists = new Claim[][] { _defaultClaims, null };
        private static readonly int[] _connectionCounts = new int[] {1, 2};

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
            var manager = new ServiceManager(new ServiceManagerOptions() { ConnectionString = _testConnectionString, ApplicationName = appName }, null);
            var tokenString =  manager.GenerateClientAccessToken(HubName, userId, claims, _tokenLifeTime);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            string expectedToken = JwtTokenHelper.GenerateExpectedAccessToken(token, GetExpectedClientEndpoint(appName), AccessKey, claims);

            Assert.Equal(expectedToken, tokenString);
        }

        [Theory]
        [MemberData(nameof(TestGenerateClientEndpointData))]
        internal void GenerateClientEndpointTest(string appName, string expectedClientEndpoint)
        {
            var manager = new ServiceManager(new ServiceManagerOptions() { ConnectionString = _testConnectionString, ApplicationName = appName }, null);
            var clientEndpoint = manager.GetClientEndpoint(HubName);

            Assert.Equal(expectedClientEndpoint, clientEndpoint);
        }

        //[Theory(Skip = "Reenable when it is ready")]
        [Theory]
        [MemberData(nameof(TestServiceManagerOptionData))]
        internal async Task CreateServiceHubContextTest(ServiceTransportType serviceTransportType, bool useLoggerFactory, string appName, int connectionCount)
        {
            var serviceManager = new ServiceManager(new ServiceManagerOptions
            {
                ConnectionString = _testConnectionString,
                ServiceTransportType = serviceTransportType,
                ApplicationName = appName,
                ConnectionCount = connectionCount
            }, null);

            using (var loggerFactory = useLoggerFactory ? (ILoggerFactory)new LoggerFactory() : NullLoggerFactory.Instance)
            {
                var hubContext = await serviceManager.CreateHubContextAsync(HubName, loggerFactory);
            }
        }

        [Theory]
        [MemberData(nameof(TestServiceManagerOptionData))]
        internal async Task CreateServiceHubContextGenericTest(ServiceTransportType serviceTransportType,
            bool useLoggerFactory, string appName, int connectionCount)
        {
            var serviceManager = new ServiceManager(new ServiceManagerOptions
            {
                ConnectionString = _testConnectionString,
                ServiceTransportType = serviceTransportType,
                ApplicationName = appName,
                ConnectionCount = connectionCount
            }, null);

            using (var loggerFactory = useLoggerFactory ? (ILoggerFactory)new LoggerFactory() : NullLoggerFactory.Instance)
            {
                var hubContext = await serviceManager.CreateHubContextAsync<IServiceHubTestClient>(HubName, loggerFactory);
            }
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
