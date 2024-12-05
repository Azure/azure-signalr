// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Azure.Identity;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

#nullable enable

[Collection("Auth")]
public class AuthUtilityTests
{
    private const string Audience = "https://localhost/aspnetclient?hub=testhub";

    private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

    [Fact]
    public void TestAccessTokenTooLongThrowsException()
    {
        var claims = GenerateClaims(100);
        var accessKey = new AccessKey(SigningKey);
        var exception = Assert.Throws<AzureSignalRAccessTokenTooLongException>(() => AuthUtility.GenerateAccessToken(
            accessKey.KeyBytes,
            accessKey.Kid,
            Audience,
            claims,
            DefaultLifetime,
            AccessTokenAlgorithm.HS256));

        Assert.Equal("AccessToken must not be longer than 4K.", exception.Message);
    }

    [Fact]
    public void TestAccessTokenHasIssuer()
    {
        var accessKey = new AccessKey(SigningKey);
        var token = AuthUtility.GenerateAccessToken(accessKey.KeyBytes,
                                                    accessKey.Kid,
                                                    Audience,
                                                    [],
                                                    DefaultLifetime,
                                                    AccessTokenAlgorithm.HS256);

        Assert.True(TokenUtilities.TryParseIssuer(token, out var iss));
        Assert.Equal(Constants.AsrsTokenIssuer, iss);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("microsoft.com")]
    public void TestTryParseIssuer(string? issuer)
    {
        var accessKey = new AccessKey(SigningKey);
        var token = AuthUtility.GenerateJwtToken(accessKey.KeyBytes, issuer: issuer);

        if (string.IsNullOrEmpty(issuer))
        {
            Assert.False(TokenUtilities.TryParseIssuer(token, out var actual));
            Assert.Null(actual);
        }
        else
        {
            Assert.True(TokenUtilities.TryParseIssuer(token, out var actual));
            Assert.Equal(issuer, actual);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123123")]
    [InlineData("foobar")]
    public void TestTryParseIssuerInvalidToken(string? jwtToken)
    {
        Assert.False(TokenUtilities.TryParseIssuer(jwtToken, out var issuer));
        Assert.Null(issuer);
    }

    private static Claim[] GenerateClaims(int count)
    {
        return Enumerable.Range(0, count).Select(s => new Claim($"ClaimSubject{s}", $"ClaimValue{s}")).ToArray();
    }

    public class CachingTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { new AccessKey(SigningKey), true };
            var key = new MicrosoftEntraAccessKey(new Uri("http://localhost"), new DefaultAzureCredential());
            key.UpdateAccessKey("foo", SigningKey);
            yield return new object[] { key, false };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
