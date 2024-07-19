// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ClientConnectionContextFacts
{
    [Fact]
    public void SetUserIdFeatureTest()
    {
        var claims = new Claim[] { new(Constants.ClaimType.UserId, "testUser") };
        var connection = new ClientConnectionContext(new("connectionId", claims));
        var feature = connection.Features.Get<ServiceUserIdFeature>();
        Assert.NotNull(feature);
        Assert.Equal("testUser", feature.UserId);
    }

    [Fact]
    public void DoNotSetUserIdFeatureWithoutUserIdClaimTest()
    {
        var connection = new ClientConnectionContext(new("connectionId", Array.Empty<Claim>()));
        var feature = connection.Features.Get<ServiceUserIdFeature>();
        Assert.Null(feature);
    }
}
