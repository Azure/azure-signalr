// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.SignalR.Tests.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceEndpointUtilityFacts
    {
        private const string Endpoint = "https://myendpoint";

        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";

        private static readonly string ConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};";

        private static readonly ServiceOptions ServiceOptions = new ServiceOptions
        {
            ConnectionString = ConnectionString
        };

        private static readonly IServiceEndpointUtility EndpointUtility = new ServiceEndpointUtility(Options.Create(ServiceOptions));

        private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new JwtSecurityTokenHandler();

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
            var tokenString = EndpointUtility.GenerateServerAccessToken<TestHub>(userId);
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}:5002/server/?hub={nameof(TestHub).ToLower()}",
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                },
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey);

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Fact]
        public void GenerateClientAccessToken()
        {
            var tokenString = EndpointUtility.GenerateClientAccessToken<TestHub>();
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}:5001/client/?hub={nameof(TestHub).ToLower()}",
                null,
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey);

            Assert.Equal(expectedTokenString, tokenString);
        }

        private string GenerateJwtBearer(string audience,
            IEnumerable<Claim> subject,
            DateTime expires,
            DateTime notBefore,
            DateTime issueAt,
            string signingKey)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            return JwtSecurityTokenHandler.WriteToken(JwtSecurityTokenHandler.CreateJwtSecurityToken(
                issuer: null,
                audience: audience,
                subject: subject == null ? null : new ClaimsIdentity(subject),
                notBefore: notBefore,
                expires: expires,
                issuedAt: issueAt,
                signingCredentials: credentials));
        }
    }
}
