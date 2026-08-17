// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ClaimsUtilityTests
    {
        private static readonly Claim[] JwtAuthenticatedClaims = new Claim[] { new("dummy", "dummy"), new("name", "name"), new("role", "admin"), new("aud", "aud") };
        private static readonly (ClaimsIdentity identity, string userId, Func<IEnumerable<Claim>> provider, string expectedAuthenticationType, int expectedClaimsCount)[] _claimsParameters =
        {
            (new ClaimsIdentity(), null, null, null, 0),
            (new ClaimsIdentity(null, null, null, null), null, null, null, 0),
            (new ClaimsIdentity("", "", ""), "", () => JwtAuthenticatedClaims, "", 4),
            (new ClaimsIdentity(), "user1", () => JwtAuthenticatedClaims, "Bearer", 4),
            (new ClaimsIdentity(JwtAuthenticatedClaims, "Bearer"), null, null, "Bearer", 4),
            (new ClaimsIdentity(JwtAuthenticatedClaims, "Bearer", "name", "role"), null, null, "Bearer", 4),
            (new ClaimsIdentity("jwt", "name", "role"), "user", () => JwtAuthenticatedClaims, "jwt", 4),
        };

        public static IEnumerable<object[]> ClaimsParameters =>
            _claimsParameters.Select(provider => new object[] { provider.identity, provider.userId, provider.provider, provider.expectedAuthenticationType, provider.expectedClaimsCount });

        [Fact]
        public void TestGetSystemClaimsWithDefaultValue()
        {
            var claims = ClaimsUtility.BuildJwtClaims(null, null, null).ToList();
            Assert.Empty(claims);
        }

        [Theory]
        [MemberData(nameof(ClaimsParameters))]
        public void TestGetSystemClaims(ClaimsIdentity identity, string userId, Func<IEnumerable<Claim>> provider, string expectedAuthenticationType, int expectedClaimsCount)
        {
            var claims = ClaimsUtility.BuildJwtClaims(new ClaimsPrincipal(identity), userId, provider).ToArray();
            var resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            var ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);

            Assert.Equal(expectedAuthenticationType, ci.AuthenticationType);

            Assert.Equal(identity.RoleClaimType, ci.RoleClaimType);
            Assert.Equal(identity.NameClaimType, ci.NameClaimType);

            Assert.Equal(expectedClaimsCount, ci.Claims.Count());
        }

        [Fact]
        public void TestGetPreservedSystemClaims()
        {
            // preserved system claims are renamed and reverted back
            var claims = ClaimsUtility.BuildJwtClaims(
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new("iss", "A"), new("jti", "B") })), null, null).ToArray();
            Assert.Equal("asrs.u.iss", claims[0].Type);
            Assert.Equal("asrs.u.jti", claims[1].Type);

            var resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            var ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);
            Assert.Equal(2, ci.Claims.Count());
            Assert.True(ci.HasClaim("iss", "A"));
            Assert.True(ci.HasClaim("jti", "B"));
        }

        [Fact]
        public void TestGetSubjectClaims()
        {
            // only the first sub claim is considered as valid to the service
            var claims = ClaimsUtility.BuildJwtClaims(
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new("sub", "A"), new("sub", "B") })), null, null).ToArray();
            Assert.Equal("sub", claims[0].Type);
            Assert.Equal("asrs.u.sub", claims[1].Type);

            var resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            var ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);
            Assert.Equal(2, ci.Claims.Count());
            Assert.True(ci.HasClaim("sub", "A"));
            Assert.True(ci.HasClaim("sub", "B"));

            claims = ClaimsUtility.BuildJwtClaims(
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new("sub", "A"), new("sub", "B") })), "C", null).ToArray();
            Assert.Equal("asrs.s.uid", claims[0].Type);
            Assert.Equal("sub", claims[1].Type);
            Assert.Equal("asrs.u.sub", claims[2].Type);

            resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);
            Assert.Equal(2, ci.Claims.Count());
            Assert.True(ci.HasClaim("sub", "A"));
            Assert.True(ci.HasClaim("sub", "B"));

            // single sub claim is considered as valid
            claims = ClaimsUtility.BuildJwtClaims(
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new("sub", "A") })), null, null).ToArray();
            Assert.Single(claims);
            Assert.Equal("sub", claims[0].Type);

            resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);
            Assert.Single(ci.Claims);
            Assert.True(ci.HasClaim("sub", "A"));

            claims = ClaimsUtility.BuildJwtClaims(
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new("sub", "A") })), "C", null).ToArray();
            Assert.Equal("asrs.s.uid", claims[0].Type);
            Assert.Equal("sub", claims[1].Type);

            resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);
            Assert.Single(ci.Claims);
            Assert.True(ci.HasClaim("sub", "A"));
        }

        [Fact]
        public void PrepareClaimsForTokenRemovesTokenClaimsAndEquivalentAliases()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "alice"),
                new Claim("nameid", "alice"),
                new Claim(ClaimTypes.Role, "admin"),
                new Claim("role", "admin"),
                new Claim("aud", "old-audience"),
                new Claim("iss", "azure-signalr"),
                new Claim("tenant", "contoso"),
                new Claim("tenant", "fabrikam"),
                new Claim(Constants.ClaimType.AuthExpiresOn, "12345"),
            };

            var prepared = ClaimsUtility.PrepareClaimsForToken(claims);

            Assert.Single(prepared, claim =>
                claim.Type is ClaimTypes.NameIdentifier or "nameid" && claim.Value == "alice");
            Assert.Single(prepared, claim =>
                claim.Type is ClaimTypes.Role or "role" && claim.Value == "admin");
            Assert.DoesNotContain(prepared, claim => claim.Type is "aud" or "iss");
            Assert.Equal(2, prepared.Count(claim => claim.Type == "tenant"));
            Assert.Contains(prepared, claim => claim.Type == Constants.ClaimType.AuthExpiresOn);
        }

        [Fact]
        public void PrepareClaimsForTokenDeduplicatesEveryOutboundClaimAlias()
        {
            foreach (var mapping in ClaimTypeMapping.OutboundClaimTypeMap)
            {
                var claims = new[]
                {
                    new Claim(mapping.Key, "value"),
                    new Claim(mapping.Value, "value"),
                };

                var prepared = ClaimsUtility.PrepareClaimsForToken(claims);

                Assert.Single(prepared);
            }
        }

        [Fact]
        public void PrepareClaimsForTokenPreservesIdenticalClaimsFromSameOriginalType()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "alice", ClaimValueTypes.String),
                new Claim(ClaimTypes.NameIdentifier, "alice", ClaimValueTypes.String),
                new Claim(ClaimTypes.NameIdentifier, "alice", ClaimValueTypes.String),
            };

            var prepared = ClaimsUtility.PrepareClaimsForToken(claims);

            Assert.Equal(3, prepared.Count);
            Assert.All(prepared, claim =>
            {
                Assert.Equal(ClaimTypes.NameIdentifier, claim.Type);
                Assert.Equal("alice", claim.Value);
                Assert.Equal(ClaimValueTypes.String, claim.ValueType);
            });
        }

        [Fact]
        public void PrepareClaimsForTokenPreservesAliasesWithDifferentValues()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "alice"),
                new Claim("nameid", "bob"),
            };

            var prepared = ClaimsUtility.PrepareClaimsForToken(claims);

            Assert.Equal(2, prepared.Count);
            Assert.Equal("alice", prepared[0].Value);
            Assert.Equal("bob", prepared[1].Value);
        }

        [Fact]
        public void PrepareClaimsForTokenPreservesAliasesWithDifferentValueTypes()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1", ClaimValueTypes.String),
                new Claim("nameid", "1", ClaimValueTypes.Integer),
            };

            var prepared = ClaimsUtility.PrepareClaimsForToken(claims);

            Assert.Equal(2, prepared.Count);
            Assert.Equal(ClaimValueTypes.String, prepared[0].ValueType);
            Assert.Equal(ClaimValueTypes.Integer, prepared[1].ValueType);
        }
    }
}
