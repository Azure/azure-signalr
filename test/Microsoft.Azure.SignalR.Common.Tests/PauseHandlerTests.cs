// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class PauseHandlerTests
{
    [Fact]
    public async Task TestPauseAndResume()
    {
        var handler = new PauseHandler();
        Assert.False(handler.ShouldReplyAck());

        await handler.PauseAsync();
        Assert.False(await handler.TryAcquire(100));

        Assert.True(handler.ShouldReplyAck());
        Assert.False(handler.ShouldReplyAck()); // ack only once

        await handler.PauseAsync(); // pause can be called multiple times.
        Assert.False(await handler.TryAcquire(100));
        Assert.False(handler.ShouldReplyAck()); // already acked previously

        await handler.ResumeAsync();

        Assert.True(await handler.TryAcquire(100));
        Assert.False(await handler.TryAcquire(100)); // only 1 parallel
        handler.Release();
        Assert.True(await handler.TryAcquire(100));
    }
}
