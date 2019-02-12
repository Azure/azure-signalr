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
        private const string HubName = "signalrbench";
        private static readonly string _testConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";
        private static readonly ServiceManagerOptions _serviceManagerOptions = new ServiceManagerOptions
        {
            ConnectionString = _testConnectionString
        };
        private static readonly ServiceManager _serviceManager = new ServiceManager(_serviceManagerOptions);

        [Fact]
        internal void GenerateClientAccessTokenTest()
        {
            var userId = "UserA";
            var expectedAudience = $"{Endpoint}/client/?hub={HubName.ToLower()}";
            var lifeTime = TimeSpan.FromSeconds(99);
            var claims = new List<Claim> { new Claim("type1", "val1") };

            var tokenString = _serviceManager.GenerateClientAccessToken(HubName, userId, claims, lifeTime);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);
            var customClaims = new Claim[] { new Claim("type1", "val1") };

            string expectedToken = JwtTokenHelper.GenerateExpectedAccessToken(token, expectedAudience, AccessKey, customClaims);

            Assert.Equal(expectedToken, tokenString);
        }

        [Fact]
        internal void GenerateClientEndpointTest()
        {
            var clientEndpoint = _serviceManager.GenerateClientEndpoint(HubName);
            var expectedClientEndpoint = $"{Endpoint}/client/?hub={HubName}";

            Assert.Equal(expectedClientEndpoint, clientEndpoint);
        }
    }
}
