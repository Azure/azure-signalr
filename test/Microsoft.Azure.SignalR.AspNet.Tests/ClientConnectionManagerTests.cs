// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

public class ClientConnectionManagerTests
{
    private readonly ClientConnectionManager _clientConnectionManager;

    private ClientConnectionManager CreateClientConnectionManager()
    {
        var hubConfig = new HubConfiguration
        {
            Resolver = new DefaultDependencyResolver()
        };
        var transport = new AzureTransportManager(hubConfig.Resolver);
        hubConfig.Resolver.Register(typeof(ITransportManager), () => transport);

        return new ClientConnectionManager(hubConfig, null);
    }

    public ClientConnectionManagerTests()
    {
        _clientConnectionManager = CreateClientConnectionManager();
    }

    [Theory]
    [InlineData("?connectionToken=invalidOne", "a")]
    [InlineData("?connectionToken=conn2:user1%32c", "conn1")]
    [InlineData("connectionToken=conn2:user1%32c", "conn3")]
    public async Task TestCreateConnectionAlwaysUsesConnectionIdInOpenConnectionMessage(string queryString, string expectedConnectionId)
    {
        var message = new OpenConnectionMessage(expectedConnectionId, new Claim[0], null, queryString);
        var connection = await _clientConnectionManager.CreateConnection(message);
        Assert.Equal(expectedConnectionId, connection.ConnectionId);
    }

    [Theory]
    [InlineData("?transport=webSockets")]
    [InlineData("transport=webSockets")]
    [InlineData("connectionToken=")]
    public async Task TestCreateConnectionWithInvalidQueryStringThrows(string queryString)
    {
        var message = new OpenConnectionMessage(Guid.NewGuid().ToString("N"), new Claim[0], null, queryString);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _clientConnectionManager.CreateConnection(message));
    }

    [Theory]
    [InlineData("?connectionToken=anotherone", "anotherone")]
    [InlineData("connectionToken=anotherone", "anotherone")]
    [InlineData("", null)]
    [InlineData("transport=webSockets", null)]
    public void TestGetHostContext(string queryString, string expectedToken)
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
        var context = _clientConnectionManager.GetHostContext(message, response);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("", ClientConnectionManager.GetContentAndDispose(response));
        Assert.Equal("value1", context.Request.Headers["custom1"]);
        Assert.Equal(expectedToken, context.Request.QueryString["connectionToken"]);
    }

    [Theory]
    [InlineData(null, null, 0, null)]
    [InlineData("Bearer", null, 2, "custom", "custom2")]
    [InlineData("Bearer", null, 2, "aud", "custom", "custom2")]
    [InlineData("Bearer", null, 1, "aud", "exp", "iat", "nbf", "custom")]
    [InlineData("Bearer", ClaimTypes.Name, 2, "aud", "exp", "iat", "nbf", "custom", ClaimTypes.Name)]
    [InlineData("aut", ClaimTypes.Name, 1, "aud", "exp", "iat", "nbf", Constants.ClaimType.AuthenticationType, ClaimTypes.Name)]
    [InlineData("aut", "nt", 2, "nt", Constants.ClaimType.NameType, Constants.ClaimType.AuthenticationType, ClaimTypes.Name)]
    public void TestGetUserPrincipal(string expectedAuthenticationType, string expectedUserName, int expectedUserClaimCount, params string[] claims)
    {
        var message = new OpenConnectionMessage(Guid.NewGuid().ToString("N"), claims?.Select(s => new Claim(s, GenerateFakeClaimValueFromKey(s))).ToArray());
        var principal = message.GetUserPrincipal();
        Assert.Equal(expectedAuthenticationType, principal.Identity.AuthenticationType);
        Assert.Equal(expectedUserName, principal.Identity.Name);
        Assert.Equal(expectedUserClaimCount, principal.Claims.Count());
    }

    private static string GenerateFakeClaimValueFromKey(string type)
    {
        if (type == null)
        {
            return null;
        }

        if (type.StartsWith(Constants.ClaimType.AzureSignalRSysPrefix))
        {
            return type.Substring(Constants.ClaimType.AzureSignalRSysPrefix.Length);
        }

        return type;
    }
}
