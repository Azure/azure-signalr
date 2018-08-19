// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;
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
        public void ServiceConnectionContextWithSystemClaimsIsUnauthenticated()
        {
            var claims = new[]
            {
                new Claim("aud", "http://localhost"),
                new Claim("exp", "1234567890"),
                new Claim("iat", "1234567890"),
                new Claim("nbf", "1234567890"),
                new Claim(Constants.ClaimType.UserId, string.Empty)
            };
            var serviceConnectionContext = new ServiceConnectionContext(new OpenConnectionMessage("1", claims));
            Assert.NotNull(serviceConnectionContext.User.Identity);
            Assert.False(serviceConnectionContext.User.Identity.IsAuthenticated);
        }

        [Fact]
        public void ServiceConnectionContextWithClaimsCreatesIdentityWithClaims()
        {
            var claims = new[]
            {
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

        [Fact]
        public void ServiceConnectionContextWithEmptyHttpContextByDefault()
        {
            var serviceConnectionContext = new ServiceConnectionContext(new OpenConnectionMessage("1", new Claim[0]));
            Assert.NotNull(serviceConnectionContext.User.Identity);
            Assert.NotNull(serviceConnectionContext.HttpContext);
            Assert.Equal(serviceConnectionContext.User, serviceConnectionContext.HttpContext.User);
            Assert.Empty(serviceConnectionContext.HttpContext.Request.Headers);
            Assert.Empty(serviceConnectionContext.HttpContext.Request.Query);
        }

        [Fact]
        public void ServiceConnectionContextWithNonEmptyHeaders()
        {
            const string key1 = "header-key-1";
            const string key2 = "header-key-2";
            const string value1 = "header-value-1";
            var value2 = new[] {"header-value-2a", "header-value-2b"};
            var serviceConnectionContext = new ServiceConnectionContext(new OpenConnectionMessage("1", new Claim[0],
                new Dictionary<string, StringValues> (StringComparer.OrdinalIgnoreCase)
                {
                    {key1, value1},
                    {key2, value2}
                }, string.Empty));

            Assert.NotNull(serviceConnectionContext.User.Identity);
            Assert.NotNull(serviceConnectionContext.HttpContext);
            Assert.Equal(serviceConnectionContext.User, serviceConnectionContext.HttpContext.User);
            Assert.Equal(2, serviceConnectionContext.HttpContext.Request.Headers.Count);
            Assert.Equal(value1, serviceConnectionContext.HttpContext.Request.Headers[key1]);
            Assert.Equal(value2, serviceConnectionContext.HttpContext.Request.Headers[key2]);
            Assert.Empty(serviceConnectionContext.HttpContext.Request.Query);
        }

        [Fact]
        public void ServiceConnectionContextWithNonEmptyQueries()
        {
            const string queryString = "?query1=value1&query2=value2&query3=value3";
            var serviceConnectionContext = new ServiceConnectionContext(new OpenConnectionMessage("1", new Claim[0],
                new Dictionary<string, StringValues>(), queryString));

            Assert.NotNull(serviceConnectionContext.User.Identity);
            Assert.NotNull(serviceConnectionContext.HttpContext);
            Assert.Equal(serviceConnectionContext.User, serviceConnectionContext.HttpContext.User);
            Assert.Empty(serviceConnectionContext.HttpContext.Request.Headers);
            Assert.Equal(queryString, serviceConnectionContext.HttpContext.Request.QueryString.ToString());
            Assert.Equal(3, serviceConnectionContext.HttpContext.Request.Query.Count);
            Assert.Equal("value1", serviceConnectionContext.HttpContext.Request.Query["query1"]);
            Assert.Equal("value2", serviceConnectionContext.HttpContext.Request.Query["query2"]);
            Assert.Equal("value3", serviceConnectionContext.HttpContext.Request.Query["query3"]);
        }
    }
}
