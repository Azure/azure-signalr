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
        [Fact]
        internal void GenerateClientAccessTokenTest()
        {
            var endpoint = "https://abc";
            var accessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
            var testConnectionString = $"Endpoint={endpoint};AccessKey={accessKey};Version=1.0;";
            var userId = "UserA";
            var hubName = "signalrbench";
            var expectedAudience = $"{endpoint}/client/?hub={hubName.ToLower()}";
            var lifeTime = TimeSpan.FromSeconds(99);
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId), new Claim("type1", "val1") };

            var serviceManagerOptions = new ServiceManagerOptions
            {
                ConnectionString = testConnectionString,
                ServiceTransportType = ServiceTransportType.Transient
            };
            var serviceManager = new ServiceManager(serviceManagerOptions);
            var tokenString = serviceManager.GenerateClientAccessToken(hubName, claims, lifeTime);
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);
            var customClaims = new Claim[] { new Claim("type1", "val1") };

            string expectedToken = JwtTokenHelper.GenerateExpectedAccessToken(token, expectedAudience, accessKey, customClaims);

            Assert.Equal(expectedToken, tokenString);
        }
    }
}
