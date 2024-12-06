// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth;

public class AccessKeyTests
{
    private const string Audience = "https://localhost/aspnetclient?hub=testhub";

    private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    [Fact]
    public async Task TestGenerateAccessToken()
    {
        var accessKey = new AccessKey(SigningKey);
        var token = await accessKey.GenerateAccessTokenAsync(Audience, [], TimeSpan.FromHours(1), AccessTokenAlgorithm.HS256);
        Assert.True(TokenUtilities.TryParseIssuer(token, out var iss));
        Assert.Equal(Constants.AsrsTokenIssuer, iss);
    }
}
