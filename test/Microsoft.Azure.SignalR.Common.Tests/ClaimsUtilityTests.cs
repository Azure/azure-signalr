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
        private static readonly Claim[] JwtAuthenticatedClaims = new Claim[] { new Claim("dummy", "dummy"), new Claim("name", "name"), new Claim("role", "admin"), new Claim("aud", "aud") };
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
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim("iss", "A"), new Claim("jti", "B") })), null, null).ToArray();
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
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim("sub", "A"), new Claim("sub", "B") })), null, null).ToArray();
            Assert.Equal("sub", claims[0].Type);
            Assert.Equal("asrs.u.sub", claims[1].Type);

            var resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            var ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);
            Assert.Equal(2, ci.Claims.Count());
            Assert.True(ci.HasClaim("sub", "A"));
            Assert.True(ci.HasClaim("sub", "B"));

            claims = ClaimsUtility.BuildJwtClaims(
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim("sub", "A"), new Claim("sub", "B") })), "C", null).ToArray();
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
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim("sub", "A") })), null, null).ToArray();
            Assert.Single(claims);
            Assert.Equal("sub", claims[0].Type);

            resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);
            Assert.Single(ci.Claims);
            Assert.True(ci.HasClaim("sub", "A"));

            claims = ClaimsUtility.BuildJwtClaims(
                new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim("sub", "A") })), "C", null).ToArray();
            Assert.Equal("asrs.s.uid", claims[0].Type);
            Assert.Equal("sub", claims[1].Type);

            resultIdentity = ClaimsUtility.GetUserPrincipal(claims).Identity;

            ci = resultIdentity as ClaimsIdentity;
            Assert.NotNull(ci);
            Assert.Single(ci.Claims);
            Assert.True(ci.HasClaim("sub", "A"));
        }
    }
}
