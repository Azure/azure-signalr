// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

public class ServiceEndpointProviderTests
{
    private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static readonly SymmetricSecurityKey SecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    private const string DefaultConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0";


    [Theory]
    [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetclient")]
    [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetclient")]
    [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetclient")]
    public async Task TestGenerateClientAccessToken(string connectionString, string expectedAudience)
    {
        var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString), new ServiceOptions() { });

        var clientToken = await provider.GenerateClientAccessTokenAsync(null, new Claim[]
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
        var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString), new ServiceOptions() { });

        var clientEndpoint = provider.GetClientEndpoint(null, originalPath, queryString);

        Assert.Equal(expectedEndpoint, clientEndpoint);
    }

    [Theory]
    [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetserver/?hub=hub1")]
    [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetserver/?hub=hub1")]
    [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetserver/?hub=hub1")]
    public async Task TestGenerateServerAccessToken(string connectionString, string expectedAudience)
    {
        var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString), new ServiceOptions() { });

        var serverToken = await provider.GetServerAccessTokenProvider("hub1", "user1").ProvideAsync();

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(serverToken, new TokenValidationParameters
        {
            ValidateIssuer = false,
            IssuerSigningKey = SecurityKey,
            ValidAudience = expectedAudience
        }, out _);

        Assert.Equal("user1", principal.FindFirst(ClaimTypes.NameIdentifier).Value);
    }

    [Theory]
    [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetserver/?hub=prefix_hub1")]
    [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost/aspnetserver/?hub=prefix_hub1")]
    [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetserver/?hub=prefix_hub1")]
    public async Task TestGenerateServerAccessTokenWIthPrefix(string connectionString, string expectedAudience)
    {
        var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString), new ServiceOptions() { ApplicationName = "prefix" });

        var serverToken = await provider.GetServerAccessTokenProvider("hub1", "user1").ProvideAsync();

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(serverToken, new TokenValidationParameters
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
        var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString), new ServiceOptions() { });
        var clientEndpoint = provider.GetServerEndpoint("hub1");
        Assert.Equal(expectedEndpoint, clientEndpoint);
    }

    [Theory]
    [InlineData("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost:8080/aspnetserver/?hub=prefix_hub1")]
    [InlineData("Endpoint=http://localhost/;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0", "http://localhost:8080/aspnetserver/?hub=prefix_hub1")]
    [InlineData("Endpoint=https://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;", "https://localhost/aspnetserver/?hub=prefix_hub1")]
    public void TestGenerateServerEndpointWithPrefix(string connectionString, string expectedEndpoint)
    {
        var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString), new ServiceOptions() { ApplicationName = "prefix" });
        var clientEndpoint = provider.GetServerEndpoint("hub1");
        Assert.Equal(expectedEndpoint, clientEndpoint);
    }

    [Theory]
    [InlineData(AccessTokenAlgorithm.HS256)]
    [InlineData(AccessTokenAlgorithm.HS512)]
    public async Task TestGenerateServerAccessTokenWithSpecifedAlgorithm(AccessTokenAlgorithm algorithm)
    {
        var connectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0";
        var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString), new ServiceOptions() { AccessTokenAlgorithm = algorithm });
        var serverToken = await provider.GetServerAccessTokenProvider("hub1", "user1").ProvideAsync();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(serverToken);

        Assert.Equal(algorithm.ToString(), token.SignatureAlgorithm);
    }

    [Theory]
    [InlineData(AccessTokenAlgorithm.HS256)]
    [InlineData(AccessTokenAlgorithm.HS512)]
    public async Task TestGenerateClientAccessTokenWithSpecifedAlgorithm(AccessTokenAlgorithm algorithm)
    {
        var connectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0";
        var provider = new ServiceEndpointProvider(new ServiceEndpoint(connectionString), new ServiceOptions() { AccessTokenAlgorithm = algorithm });
        var generatedToken = await provider.GenerateClientAccessTokenAsync("hub1");

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(generatedToken);

        Assert.Equal(algorithm.ToString(), token.SignatureAlgorithm);
    }

    [Fact(Skip = "Access token does not need to be unique")]
    public async Task GenerateMultipleAccessTokenShouldBeUnique()
    {
        var sep = new ServiceEndpointProvider(new ServiceEndpoint(DefaultConnectionString), new ServiceOptions() { });
        var userId = Guid.NewGuid().ToString();
        var tokens = new List<string>();

        var count = 1000;
        for (var i = 0; i < count; i++)
        {
            tokens.Add(await sep.GenerateClientAccessTokenAsync());
            tokens.Add(await sep.GetServerAccessTokenProvider("test1", userId).ProvideAsync());
        }

        var distinct = tokens.Distinct();
        Assert.Equal(tokens.Count, distinct.Count());
    }
}
