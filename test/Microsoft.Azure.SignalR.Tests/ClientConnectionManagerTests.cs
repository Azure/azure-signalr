// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class ClientConnectionManagerTests
{
    [Fact]
    public async Task TestAllClientConnectionsCompleted()
    {
        var manager = new ClientConnectionManager();

        var c1 = new ClientConnectionContext(new Protocol.OpenConnectionMessage("foo", Array.Empty<Claim>()));
        var c2 = new ClientConnectionContext(new Protocol.OpenConnectionMessage("bar", Array.Empty<Claim>()));

        manager.TryAddClientConnection(c1);
        manager.TryAddClientConnection(c2);

        _ = RemoveConnection(manager, c1);
        _ = RemoveConnection(manager, c2);

        var expected = manager.WhenAllCompleted();
        var actual = await Task.WhenAny(
            expected,
            Task.Delay(TimeSpan.FromSeconds(1))
        );
        Assert.Equal(expected, actual);
    }

    private static async Task RemoveConnection(IClientConnectionManager _, ClientConnectionContext ctx)
    {
        await Task.Delay(100);
        ctx.OnCompleted();
    }

}
