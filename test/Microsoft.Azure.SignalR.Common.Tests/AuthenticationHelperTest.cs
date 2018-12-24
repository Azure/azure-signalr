﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class AuthenticationHelperTest
    {
        private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private const string DefaultConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0";
        private const string Audience = "https://localhost/aspnetclient?hub=testhub";
        private static TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

        [Fact]
        public void TestAccessTokenTooLongThrowsException()
        {
            var claims = GenerateClaims(100);
            var exception = Assert.Throws<AzureSignalRAccessTokenTooLongException>(() => AuthenticationHelper.GenerateAccessToken(SigningKey, Audience, claims, DefaultLifetime, string.Empty));

            Assert.Equal("AccessToken must not be longer than 4K.", exception.Message);
        }

        private Claim[] GenerateClaims(int count)
        {
            return Enumerable.Range(0, count).Select(s => new Claim($"ClaimSubject{s}", $"ClaimValue{s}")).ToArray();
        }
    }
}
