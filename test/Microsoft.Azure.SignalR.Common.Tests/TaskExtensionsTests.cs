// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class TaskExtensionsTests
    {
        [Fact]
        public async Task TestCancelCouldThrow()
        {
            var task = Task.Delay(10000);
            var cts = new CancellationTokenSource();
            var taskOrCancel = task.OrCancelAsync(cts.Token);
            Assert.False(taskOrCancel.IsCompleted);
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => taskOrCancel.OrTimeout());
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
}
