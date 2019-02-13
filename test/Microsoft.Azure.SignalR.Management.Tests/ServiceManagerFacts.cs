// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.Azure.SignalR.Tests;
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
        private static readonly string _testConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";
        private static readonly TimeSpan _tokenLifeTime = TimeSpan.FromSeconds(99);
        private static readonly ServiceManagerOptions _serviceManagerOptions = new ServiceManagerOptions
        {
            ConnectionString = _testConnectionString
        };
        private static readonly ServiceManager _serviceManager = new ServiceManager(_serviceManagerOptions);
        private static readonly Claim[] _defaultClaims = new Claim[] { new Claim("type1", "val1") };
        public static IEnumerable<object[]> TestGenerateAccessTokenData = new object[][]
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
    }
}
