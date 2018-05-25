// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Security.Claims;
using Microsoft.Azure.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceContextFacts
    {
        [Fact]
        public void ServiceConnectionContextWithEmptyClaimsIsUnauthenticated()
        {
            var serviceConnectionContext = new ServiceConnectionContext(new OpenConnectionMessage("1", new Claim[0]));
            Assert.NotNull(serviceConnectionContext.User.Identity);
            Assert.False(serviceConnectionContext.User.Identity.IsAuthenticated);
        }

        [Fact]
        public void ServiceConnectionContextWithNullClaimsIsUnauthenticated()
        {
            var serviceConnectionContext = new ServiceConnectionContext(new OpenConnectionMessage("1", null));
            Assert.NotNull(serviceConnectionContext.User.Identity);
            Assert.False(serviceConnectionContext.User.Identity.IsAuthenticated);
        }

        [Fact]
        public void ServiceConnectionContextWithClaimsCreatesIdentityWithClaims()
        {
            var claims = new Claim[] {
                new Claim("k1", "v1"),
                new Claim("k2", "v2")
            };
            var serviceConnectionContext = new ServiceConnectionContext(new OpenConnectionMessage("1", claims));
            Assert.NotNull(serviceConnectionContext.User.Identity);
            Assert.True(serviceConnectionContext.User.Identity.IsAuthenticated);
            var contextClaims = serviceConnectionContext.User.Claims.ToList();
            Assert.Equal("k1", contextClaims[0].Type);
            Assert.Equal("v1", contextClaims[0].Value);
            Assert.Equal("k2", contextClaims[1].Type);
            Assert.Equal("v2", contextClaims[1].Value);
        }
    }
}
