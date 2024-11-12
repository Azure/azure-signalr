// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace Microsoft.Azure.SignalR.Common.Tests;

public class TaskExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("exception")]
    public async Task TestCancelCouldThrow(string? message)
    {
        var task = Task.Delay(10000);
        var cts = new CancellationTokenSource();
        var taskOrCancel = message == null ? task.OrCancelAsync(cts.Token) : task.OrCancelAsync(cts.Token, message);
        Assert.False(taskOrCancel.IsCompleted);
        cts.Cancel();
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => taskOrCancel.OrTimeout());
        Assert.Null(exception.Task);
        Assert.Equal(message ?? "A task was canceled.", exception.Message);
    }

    [Fact]
    public async Task TestCancelCouldSucceed()
    {
        var task = Task.Delay(500);
        var cts = new CancellationTokenSource();
        var taskOrCancel = task.OrCancelAsync(cts.Token);
        Assert.False(taskOrCancel.IsCompleted);
        await taskOrCancel.OrTimeout();
    }

    [Fact]
    public async Task TestTaskWithCancelCouldThrow()
    {
        var task = Task.Run(async () =>
        {
            await Task.Delay(500);
            throw new InvalidOperationException();
        });
        var cts = new CancellationTokenSource();
        var taskOrCancel = task.OrCancelAsync(cts.Token);
        Assert.False(taskOrCancel.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => taskOrCancel.OrTimeout());
    }
}
