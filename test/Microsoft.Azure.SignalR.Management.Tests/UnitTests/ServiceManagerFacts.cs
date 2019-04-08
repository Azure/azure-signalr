// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceManagerFacts
    {
        private const string Endpoint = "https://abc";
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private const string HubName = "signalrBench";
        private const string UserId = "UserA";
        private static readonly string _clientEndpoint = $"{Endpoint}/client/?hub={HubName.ToLower()}";
        private static readonly string _clientEndpointWithAppName = $"{Endpoint}/client/?hub={_appName.ToLower()}_{HubName.ToLower()}";
        private static readonly string _testConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";
        private static readonly TimeSpan _tokenLifeTime = TimeSpan.FromSeconds(99);
        private static readonly ServiceManagerOptions _serviceManagerOptions = new ServiceManagerOptions
        {
            ConnectionString = _testConnectionString,
           
        };
        private static readonly ServiceManager _serviceManager = new ServiceManager(_serviceManagerOptions);
        private static readonly Claim[] _defaultClaims = new Claim[] { new Claim("type1", "val1") };

        private static readonly ServiceTransportType[] _serviceTransportTypes = new ServiceTransportType[]
        {
            ServiceTransportType.Transient,
            ServiceTransportType.Persistent
        };

        private static readonly bool[] _useLoggerFatories = new bool[]
        {
            false,
            true
        };
      
        private static readonly string[] _appNames = new string[]
        {
            "appName",
            "",
            null
        };

        public static readonly IEnumerable<object[]> TestGenerateAccessTokenData = new object[][]
        {
            new object[]
            {
                null,
                null
            },
            new object[]
            {
                UserId,
                null
            },
            new object[]
            {
                null,
               _defaultClaims
            },
            new object[]
            {
                UserId,
                _defaultClaims
            }
        };

        public static IEnumerable<object[]> TestServiceOptionData => from transport in _serviceTransportTypes
                                                                     from useLoggerFactory in _useLoggerFatories
                                                                     from appName in _appNames
                                                                     select new object[] { transport, useLoggerFactory, appName};

        [Theory]
        [MemberData(nameof(TestGenerateAccessTokenData))]
        internal void GenerateClientAccessTokenTest(string userId, Claim[] claims)
        {
            var tokenString = _serviceManager.GenerateClientAccessToken(HubName, userId, claims, _tokenLifeTime);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

            string expectedToken = JwtTokenHelper.GenerateExpectedAccessToken(token, _clientEndpoint, AccessKey, claims);

            Assert.Equal(expectedToken, tokenString);
        }

        [Fact]
        internal void GenerateClientEndpointTest()
        {
            var clientEndpoint = _serviceManager.GetClientEndpoint(HubName);

            Assert.Equal(_clientEndpoint, clientEndpoint);
        }

        [Theory]
        [MemberData(nameof(TestServiceOptionData))]
        internal async Task CreateServiceHubContextTest(ServiceTransportType serviceTransportType, bool useLoggerFacory, string appName)
        {
            var serviceManager = new ServiceManager(new ServiceManagerOptions
            {
                ConnectionString = _testConnectionString,
                ServiceTransportType = serviceTransportType,
                ApplicationName = appName
            });

            LoggerFactory loggerFactory;

            using (loggerFactory = useLoggerFacory ? new LoggerFactory() : null)
            {
                var hubContext = await serviceManager.CreateHubContextAsync(HubName, loggerFactory);
            }
        }
    }
}
