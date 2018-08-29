// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceEndpointProviderFacts
    {
        private const string Endpoint = "https://myendpoint";
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private static readonly string HubName = nameof(TestHub).ToLower();

        private static readonly string PreviewConnectionStringWithoutVersion =
            $"Endpoint={Endpoint};AccessKey={AccessKey};";

        private static readonly string PreviewConnectionStringWithVersion =
            $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0-preview";

        private static readonly string V1ConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0";

        private static readonly ServiceEndpointProvider[] PreviewEndpointProviderArray =
        {
            new ServiceEndpointProvider(
                Options.Create(new ServiceOptions {ConnectionString = PreviewConnectionStringWithoutVersion})),
            new ServiceEndpointProvider(
                Options.Create(new ServiceOptions {ConnectionString = PreviewConnectionStringWithVersion}))
        };

        private static readonly IServiceEndpointProvider V1EndpointProvider =
            new ServiceEndpointProvider(Options.Create(new ServiceOptions { ConnectionString = V1ConnectionString }));

        private static readonly JwtSecurityTokenHandler JwtSecurityTokenHandler = new JwtSecurityTokenHandler();

        private static readonly (string path, string expectedQuery)[] OriginalPathArray =
        {
            ("", ""),
            (null, ""),
            ("/user/path", $"&{Constants.QueryParameter.OriginalPath}=%2Fuser%2Fpath")
        };

        public static IEnumerable<object[]> PreviewEndpointProviders =>
            PreviewEndpointProviderArray.Select(provider => new object[] {provider});

        public static IEnumerable<object[]> OriginalPaths =>
            OriginalPathArray.Select(t => new object[] {t.path, t.expectedQuery});

        public static IEnumerable<object[]> PreviewEndpointProvidersWithPath =>
            from provider in PreviewEndpointProviderArray
            from t in OriginalPathArray
            select new object[] { provider, t.path, t.expectedQuery} ;

        [Theory]
        [MemberData(nameof(PreviewEndpointProviders))]
        internal void GetPreviewServerEndpoint(IServiceEndpointProvider provider)
        {
            var expected = $"{Endpoint}:5002/server/?hub={HubName}";
            var actual = provider.GetServerEndpoint<TestHub>();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(PreviewEndpointProvidersWithPath))]
        internal void GetPreviewClientEndpoint(IServiceEndpointProvider provider, string originalPath, string expectedQueryString)
        {
            var expected = $"{Endpoint}:5001/client/?hub={HubName}{expectedQueryString}";
            var actual = provider.GetClientEndpoint(HubName, originalPath);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(PreviewEndpointProviders))]
        internal void GeneratePreviewServerAccessToken(IServiceEndpointProvider provider)
        {
            const string userId = "UserA";
            var tokenString = provider.GenerateServerAccessToken<TestHub>(userId);
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}:5002/server/?hub={HubName}",
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
        [MemberData(nameof(PreviewEndpointProviders))]
        internal void GeneratePreviewClientAccessToken(IServiceEndpointProvider provider)
        {
            var tokenString = provider.GenerateClientAccessToken(HubName);
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}:5001/client/?hub={HubName}",
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
            var expected = $"{Endpoint}/server/?hub={HubName}";
            var actual = V1EndpointProvider.GetServerEndpoint<TestHub>();
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(OriginalPaths))]
        public void GetV1ClientEndpoint(string originalPath, string expectedQueryString)
        {
            var expected = $"{Endpoint}/client/?hub={HubName}{expectedQueryString}";
            var actual = V1EndpointProvider.GetClientEndpoint(HubName, originalPath);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GenerateV1ServerAccessToken()
        {
            const string userId = "UserA";
            var tokenString = V1EndpointProvider.GenerateServerAccessToken<TestHub>(userId);
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}/server/?hub={HubName}",
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
            var tokenString = V1EndpointProvider.GenerateClientAccessToken(HubName);
            var token = JwtSecurityTokenHandler.ReadJwtToken(tokenString);

            var expectedTokenString = GenerateJwtBearer($"{Endpoint}/client/?hub={HubName}",
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
