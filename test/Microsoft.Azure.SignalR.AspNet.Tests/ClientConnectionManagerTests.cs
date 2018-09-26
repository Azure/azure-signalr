// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class ClientConnectionManagerTests
    {
        private readonly ClientConnectionManager _clientConnectionManager;

        public ClientConnectionManagerTests()
        {
            var hubConfig = new HubConfiguration();
            var protectedData = new EmptyProtectedData();
            var transport = new AzureTransportManager(hubConfig.Resolver);
            hubConfig.Resolver.Register(typeof(IProtectedData), () => protectedData);
            hubConfig.Resolver.Register(typeof(ITransportManager), () => transport);

            _clientConnectionManager = new ClientConnectionManager(hubConfig);
        }

        [Theory]
        [InlineData("?transport=webSockets")]
        [InlineData("?connectionToken=invalidOne")]
        public void TestCreateConnectionWithCustomQueryStringSucceeds(string queryString)
        {
            var message = new OpenConnectionMessage(Guid.NewGuid().ToString("N"), new Claim[0], null, queryString);
            var connection = _clientConnectionManager.CreateConnection(message, null);
        }

        [Theory]
        [InlineData("?connectionToken=anotherone")]
        [InlineData("")]
        [InlineData("transport=webSockets")]
        public void TestGetHostContext(string queryString)
        {
            var message = new OpenConnectionMessage(Guid.NewGuid().ToString("N"),
                new Claim[] {
                    new Claim(ClaimTypes.Name, "user1")
                },
                new Dictionary<string, StringValues>
                {
                    ["custom1"] = "value1"
                }
                , queryString);
            var response = new MemoryStream();
            var context = _clientConnectionManager.GetHostContext(message, response, null);
            Assert.Equal(200, context.Response.StatusCode);
            Assert.Equal("", ClientConnectionManager.GetContentAndDispose(response));
            Assert.Equal("value1", context.Request.Headers["custom1"]);
            Assert.Equal($"{message.ConnectionId}:user1", context.Request.QueryString["connectionToken"]);
        }

        [Theory]
        [InlineData("Bearer", null, 2, "custom", "custom2")]
        [InlineData("Bearer", null, 2, "aud", "custom", "custom2")]
        [InlineData("Bearer", null, 1, "aud", "exp", "iat", "nbf", "custom")]
        [InlineData("Bearer", ClaimTypes.Name, 2, "aud", "exp", "iat", "nbf", "custom", ClaimTypes.Name)]
        [InlineData(Constants.ClaimType.AuthenticationType, ClaimTypes.Name, 1, "aud", "exp", "iat", "nbf", Constants.ClaimType.AuthenticationType, ClaimTypes.Name)]
        public void TestGetUserPrincipal(string expectedAuthenticationType, string expectedUserName, int expectedUserClaimCount, params string[] claims)
        {
            var message = new OpenConnectionMessage(Guid.NewGuid().ToString("N"), claims.Select(s => new Claim(s, s)).ToArray());
            var principal = ClientConnectionManager.GetUserPrincipal(message);
            Assert.Equal(expectedAuthenticationType, principal.Identity.AuthenticationType);
            Assert.Equal(expectedUserName, principal.Identity.Name);
            Assert.Equal(expectedUserClaimCount, principal.Claims.Count());
        }
    }
}
