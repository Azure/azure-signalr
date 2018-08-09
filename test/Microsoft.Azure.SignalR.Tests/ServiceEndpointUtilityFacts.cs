// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceEndpointUtilityFacts
    {
        private const string Endpoint = "https://myendpoint";

        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";

        public static IEnumerable<object[]> PreviewEndpointUtility()
        {
            yield return new object[]
            {
                new ServiceEndpointUtility(
                    Options.Create(
                        new ServiceOptions
                        {
                            ConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};"
                        }))
            };
            yield return new object[]
            {
                new ServiceEndpointUtility(
                    Options.Create(
                        new ServiceOptions
                        {
                            ConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0-preview"
                        }))
            };
        }

        private static readonly IServiceEndpointUtility V1EndpointUtility =
            new ServiceEndpointUtility(
                Options.Create(
                    new ServiceOptions
                    {
                        ConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0"
                    }));

        private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new JwtSecurityTokenHandler();

        [Theory]
        [MemberData(nameof(PreviewEndpointUtility))]
        internal void GetPreviewServerEndpoint(IServiceEndpointUtility utility)
        {
            var expected = $"{Endpoint}:5002/server/?hub={nameof(TestHub).ToLower()}";
            var actual = utility.GetServerEndpoint<TestHub>();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(PreviewEndpointUtility))]
        internal void GetPreviewClientEndpoint(IServiceEndpointUtility utility)
        {
            var expected = $"{Endpoint}:5001/client/?hub={nameof(TestHub).ToLower()}";
            var actual = utility.GetClientEndpoint<TestHub>();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(PreviewEndpointUtility))]
        internal void GeneratePreviewServerAccessToken(IServiceEndpointUtility utility)
        {
            const string userId = "UserA";
            var tokenString = utility.GenerateServerAccessToken<TestHub>(userId);
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

        [Theory]
        [MemberData(nameof(PreviewEndpointUtility))]
        internal void GeneratePreviewClientAccessToken(IServiceEndpointUtility utility)
        {
            var tokenString = utility.GenerateClientAccessToken<TestHub>();
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}:5001/client/?hub={nameof(TestHub).ToLower()}",
                null,
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                AccessKey);

            Assert.Equal(expectedTokenString, tokenString);
        }

        [Fact]
        public void GetV1ServerEndpoint()
        {
            var expected = $"{Endpoint}/server/?hub={nameof(TestHub).ToLower()}";
            var actual = V1EndpointUtility.GetServerEndpoint<TestHub>();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetV1ClientEndpoint()
        {
            var expected = $"{Endpoint}/client/?hub={nameof(TestHub).ToLower()}";
            var actual = V1EndpointUtility.GetClientEndpoint<TestHub>();
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GenerateV1ServerAccessToken()
        {
            const string userId = "UserA";
            var tokenString = V1EndpointUtility.GenerateServerAccessToken<TestHub>(userId);
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}/server/?hub={nameof(TestHub).ToLower()}",
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
        public void GenerateV1ClientAccessToken()
        {
            var tokenString = V1EndpointUtility.GenerateClientAccessToken<TestHub>();
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}/client/?hub={nameof(TestHub).ToLower()}",
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
