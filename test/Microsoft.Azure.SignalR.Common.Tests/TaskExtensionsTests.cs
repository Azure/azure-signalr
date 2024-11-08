// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class TaskExtensionsTests
    {
        [Fact]
        public async Task TestOrSilentCancelAsyncCouldCancel()
        {
            var task = Task.Delay(10000);
            var cts = new CancellationTokenSource();
            var taskOrCancel = task.OrSilentCancelAsync(cts.Token);
            Assert.False(taskOrCancel.IsCompleted);
            cts.Cancel();
            var returnTask = await taskOrCancel.OrTimeout(500);
            Assert.True(returnTask.IsCompleted);
            Assert.False(task.IsCompleted);
            Assert.NotEqual(task, returnTask);
        }

        [Fact]
        public async Task TestOrSilentCancelAsyncCouldSucceed()
        {
            var task = Task.Delay(1);
            var cts = new CancellationTokenSource();
            var taskOrCancel = task.OrSilentCancelAsync(cts.Token);
            var returnTask = await taskOrCancel.OrTimeout(500);
            Assert.True(returnTask.IsCompleted);
            Assert.True(task.IsCompleted);
            Assert.Equal(task, returnTask);
        }
    }
}
