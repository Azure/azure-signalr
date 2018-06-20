// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Azure.SignalR.Tests.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceEndpointUtilityFacts
    {
        private static readonly string Endpoint = "https://myendpoint";

        private static readonly string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";

        private static readonly string ConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};";

        private static readonly ServiceOptions ServiceOptions = new ServiceOptions
        {
            ConnectionString = ConnectionString
        };

        private static readonly IServiceEndpointUtility EndpointUtility = new ServiceEndpointUtility(Options.Create(ServiceOptions));

        [Fact]
        public void GetServerEndpoint()
        {
            string endpoint = EndpointUtility.GetServerEndpoint<TestHub>();
            Assert.Equal(endpoint, $"{Endpoint}:5002/server/?hub={nameof(TestHub).ToLower()}");
        }

        [Fact]
        public void GetClientEndpoint()
        {
            string endpoint = EndpointUtility.GetClientEndpoint<TestHub>();
            Assert.Equal(endpoint, $"{Endpoint}:5001/client/?hub={nameof(TestHub).ToLower()}");
        }

        [Fact]
        public void GenerateServerAccessToken()
        {
            string userId = "UserA";
            var token = EndpointUtility.GenerateServerAccessToken<TestHub>(userId);
            var expireDate = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
            Assert.Equal(token, AuthenticationHelper.GenerateJwtBearer(
                audience: $"{Endpoint}:5002/server/?hub={nameof(TestHub).ToLower()}",
                claims: new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    },
                expires: expireDate,
                signingKey: AccessKey
            ));
        }

        [Fact]
        public void GenerateClientAccessToken()
        {
            var token = EndpointUtility.GenerateClientAccessToken<TestHub>();
            var expireDate = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
            Assert.Equal(token, AuthenticationHelper.GenerateJwtBearer(
                audience: $"{Endpoint}:5001/client/?hub={nameof(TestHub).ToLower()}",
                claims: null,
                expires: expireDate,
                signingKey: AccessKey
            ));
        }
    }
}
