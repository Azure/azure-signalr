// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class TaskExtensionFacts
    {
        [Fact]
        public async Task TaskCompletedSuccufullyFirstTest()
        {
            await Task.CompletedTask.OrTimeout();
        }

        [Fact]
        public async Task TaskTimeoutTest()
        {
            var task = Task.Delay(Timeout.Infinite);
            await Assert.ThrowsAsync<TimeoutException>(() => task.OrTimeout(TimeSpan.FromMilliseconds(1)));
        }

        [Fact]
        public async Task TokenCancelledTest()
        {
            var task = Task.Delay(Timeout.Infinite);
            var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(() => task.OrTimeout(cts.Token));
        }

        [Fact]
        public async Task TaskCompleted_TokenCancelled_Test()
        {
            var task = Task.CompletedTask;
            var cts = new CancellationTokenSource();
            cts.Cancel();
            await task.OrTimeout(cts.Token);
        }
    }
}