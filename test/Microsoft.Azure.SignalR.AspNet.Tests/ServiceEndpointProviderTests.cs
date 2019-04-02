// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class ServiceEndpointProviderTests
    {
        private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private static readonly SymmetricSecurityKey SecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        private const string DefaultConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0";


        [Theory]
        [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetclient")]
        [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetclient")]
        [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetclient")]
        public void TestGenerateClientAccessToken(string connectionString, string expectedAudience)
        {
            var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString));

            var clientToken = provider.GenerateClientAccessToken(null, new Claim[]
            {
                new Claim("type1", "value1")
            });

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(clientToken, new TokenValidationParameters
            {
                ValidateIssuer = false,
                IssuerSigningKey = SecurityKey,
                ValidAudience = expectedAudience
            }, out var token);

            var customClaims = principal.FindAll("type1").ToList();
            Assert.Single(customClaims);
            Assert.Equal("value1", customClaims[0].Value);
        }

        [Theory]
        [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", null, null, "http://localhost:8080/aspnetclient")]
        [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "", "", "http://localhost:8080/aspnetclient")]
        [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "", "?a=b&c=d", "https://localhost/aspnetclient?a=b&c=d")]
        [InlineData("Endpoint=https://abc.com;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "orig<inal.com", "?abc=%21dcf", "https://abc.com/aspnetclient?abc=%21dcf&asrs.op=orig%3Cinal.com")]
        public void TestGenerateClientEndpoint(string connectionString, string originalPath, string queryString, string expectedEndpoint)
        {
            var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString));

            var clientEndpoint = provider.GetClientEndpoint(null, originalPath, queryString);

            Assert.Equal(expectedEndpoint, clientEndpoint);
        }

        [Theory]
        [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetserver/?hub=hub1")]
        [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetserver/?hub=hub1")]
        [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetserver/?hub=hub1")]
        public void TestGenerateServerAccessToken(string connectionString, string expectedAudience)
        {
            var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString));

            var clientToken = provider.GenerateServerAccessToken("hub1", "user1");

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(clientToken, new TokenValidationParameters
            {
                ValidateIssuer = false,
                IssuerSigningKey = SecurityKey,
                ValidAudience = expectedAudience
            }, out var token);

            Assert.Equal("user1", principal.FindFirst(ClaimTypes.NameIdentifier).Value);
        }

        [Theory]
        [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetserver/?hub=prefix_hub1")]
        [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetserver/?hub=prefix_hub1")]
        [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetserver/?hub=prefix_hub1")]
        public void TestGenerateServerAccessTokenWIthPrefix(string connectionString, string expectedAudience)
        {
            var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString, applicationName: "prefix"));

            var clientToken = provider.GenerateServerAccessToken("hub1", "user1");

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(clientToken, new TokenValidationParameters
            {
                ValidateIssuer = false,
                IssuerSigningKey = SecurityKey,
                ValidAudience = expectedAudience
            }, out var token);

            Assert.Equal("user1", principal.FindFirst(ClaimTypes.NameIdentifier).Value);
        }

        [Theory]
        [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost:8080/aspnetserver/?hub=hub1")]
        [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost:8080/aspnetserver/?hub=hub1")]
        [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetserver/?hub=hub1")]
        public void TestGenerateServerEndpoint(string connectionString, string expectedEndpoint)
        {
            var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString));

            var clientEndpoint = provider.GetServerEndpoint("hub1");

            Assert.Equal(expectedEndpoint, clientEndpoint);
        }

        [Theory]
        [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost:8080/aspnetserver/?hub=prefix_hub1")]
        [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost:8080/aspnetserver/?hub=prefix_hub1")]
        [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetserver/?hub=prefix_hub1")]
        public void TestGenerateServerEndpointWithPrefix(string connectionString, string expectedEndpoint)
        {
            var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString, applicationName: "prefix"));

            var clientEndpoint = provider.GetServerEndpoint("hub1");

            Assert.Equal(expectedEndpoint, clientEndpoint);
        }

        [Fact]
        public void GenerateMutlipleAccessTokenShouldBeUnique()
        {
            var count = 1000;
            var sep = new ServiceEndpointProvider(new ServiceEndpoint(DefaultConnectionString));
            var userId = Guid.NewGuid().ToString();
            var tokens = new List<string>();
            for (int i = 0; i < count; i++)
            {
                tokens.Add(sep.GenerateClientAccessToken());
                tokens.Add(sep.GenerateServerAccessToken("test1", userId));
            }

            var distinct = tokens.Distinct();
            Assert.Equal(tokens.Count, distinct.Count());
        }
    }
}
