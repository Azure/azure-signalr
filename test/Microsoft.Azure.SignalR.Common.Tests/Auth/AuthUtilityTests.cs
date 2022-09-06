﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Azure.Identity;
using Microsoft.IdentityModel.Tokens;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
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
            var accessKey = new AccessKey("http://localhost:443", SigningKey);
            var exception = Assert.Throws<AzureSignalRAccessTokenTooLongException>(() => AuthUtility.GenerateAccessToken(accessKey, Audience, claims, DefaultLifetime, AccessTokenAlgorithm.HS256));

            Assert.Equal("AccessToken must not be longer than 4K.", exception.Message);
        }

        private static Claim[] GenerateClaims(int count)
        {
            return Enumerable.Range(0, count).Select(s => new Claim($"ClaimSubject{s}", $"ClaimValue{s}")).ToArray();
        }

        public class CachingTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { new AccessKey("http://localhost:443", SigningKey), true };
                var key = new AadAccessKey(new Uri("http://localhost"), new DefaultAzureCredential());
                key.UpdateAccessKey("foo", SigningKey);
                yield return new object[] { key, false };
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
