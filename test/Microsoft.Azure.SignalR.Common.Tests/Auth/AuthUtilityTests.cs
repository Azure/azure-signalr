// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AuthUtilityTests
    {
        private const string Audience = "https://localhost/aspnetclient?hub=testhub";
        private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(1);

        [Fact]
        public void TestAccessTokenTooLongThrowsException()
        {
            var claims = GenerateClaims(100);
            var endpoint = new Uri("https://localhost:443");
            var accessKey = new AccessKey(endpoint, SigningKey);
            var exception = Assert.Throws<AzureSignalRAccessTokenTooLongException>(() => AuthUtility.GenerateAccessToken(accessKey, Audience, claims, DefaultLifetime, AccessTokenAlgorithm.HS256));

            Assert.Equal("AccessToken must not be longer than 4K.", exception.Message);
        }

        [Fact]
        public void TestGenerateJwtBearerCaching()
        {
            var count = 0;
            while (count < 1000)
            {
                var endpoint = new Uri("http://localhost:443");
                var accessKey = new AccessKey(endpoint, SigningKey);
                AuthUtility.GenerateJwtBearer(audience: Audience, expires: DateTime.UtcNow.Add(DefaultLifetime), signingKey: accessKey);
                count++;
            };

            // New a key to fetch cached CryptoProviderCache, it won't add to cache until CreateJwtSecurityToken
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));

            var cache = securityKey.CryptoProviderFactory.CryptoProviderCache;
            var value = cache.GetType().GetField("_signingSignatureProviders", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(cache);

            var signingProviders = value as ConcurrentDictionary<string, SignatureProvider>;
            
            // Validate same signing key cache once. 
            Assert.Single(signingProviders);
        }

        private Claim[] GenerateClaims(int count)
        {
            return Enumerable.Range(0, count).Select(s => new Claim($"ClaimSubject{s}", $"ClaimValue{s}")).ToArray();
        }
    }
}
