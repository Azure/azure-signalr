// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class ClaimsUtilityTests
    {
        private static readonly Claim[] JwtAuthenticatedClaims = new Claim[] { new Claim("dummy", "dummy"), new Claim("name", "name"), new Claim("role", "admin") };
        private static readonly (ClaimsIdentity identity, string userId, Func<IEnumerable<Claim>> provider, string expectedAuthenticationType, int expectedClaimsCount)[] _claimsParameters =
        {
            (new ClaimsIdentity(), null, null, null, 0),
            (new ClaimsIdentity(null, null, null, null), null, null, null, 0),
            (new ClaimsIdentity("", "", ""), "", () => JwtAuthenticatedClaims, "", 3),
            (new ClaimsIdentity(), "user1", () => JwtAuthenticatedClaims, "Bearer", 3),
            (new ClaimsIdentity(JwtAuthenticatedClaims, "Bearer"), null, null, "Bearer", 3),
            (new ClaimsIdentity(JwtAuthenticatedClaims, "Bearer", "name", "role"), null, null, "Bearer", 3),
            (new ClaimsIdentity("jwt", "name", "role"), "user", () => JwtAuthenticatedClaims, "jwt", 3),
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
    }
}
