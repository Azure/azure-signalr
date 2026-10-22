// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class CustomizedTimerTests(ITestOutputHelper output) : VerifiableLoggedTest(output)
{
    private const int BasePeriodMs = 400;

    private static readonly TimeSpan BaseTs = TimeSpan.FromMilliseconds(BasePeriodMs);

    private static readonly TimeSpan BaseTsPlus = TimeSpan.FromMilliseconds(BasePeriodMs * 1.2); // +20% leeway to avoid false positives

    [RetryTheory]
    // #stops == #starts
    [InlineData(0, 1, 1, 0)]
    [InlineData(3, 1, 1, 3)]
    // #starts < #stops
    [InlineData(1, 2, 3, 1)]
    // #starts > #stops
    [InlineData(1, 3, 2, 2)]
    public async Task BasicStartStopTest(int timerTicks, int numStarts, int numStops, int expectedCallbacks)
    {
        var loggerFactory = NullLoggerFactory.Instance;

        await RetryWhenExceptionThrows(async () =>
        {
            var callbackCount = 0;
            using var timer = CreatePingTimer(loggerFactory, () => Interlocked.Increment(ref callbackCount));

            for (var i = 0; i < numStarts; i++)
            {
                timer.Start();
            }
            await Task.Delay(BaseTsPlus * timerTicks);
            for (var i = 0; i < numStops; i++)
            {
                timer.Stop();
            }

            // special case check when numStops < numStarts
            Assert.Equal(numStarts <= numStops ? expectedCallbacks : timerTicks, callbackCount);

            await Task.Delay(BaseTsPlus * timerTicks);
            Assert.Equal(expectedCallbacks, callbackCount);
        });
    }

    [Fact(Skip = "Flaky test, need fixing.")]
    public async Task StartStopStartStop()
    {
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            // it will fail in osx in github action, due to slow machine.
            // skip it for now.
            return;
        }
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
        {
            var callbackCount = 0;
            using var timer = CreatePingTimer(loggerFactory, () => Interlocked.Increment(ref callbackCount));

            timer.Start();
            await Task.Delay(BaseTsPlus);
            timer.Stop();
            Assert.Equal(1, callbackCount);

            await Task.Delay(BaseTsPlus * 5);

            timer.Start();
            await Task.Delay(BaseTsPlus);
            timer.Stop();
            Assert.Equal(2, callbackCount);
        }
    }

    [Fact]
    public async Task StartStopDispose_StartDisposeStop()
    {
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            // it will fail in osx in github action, due to slow machine.
            // skip it for now.
            return;
        }
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
        {
            var callbackCount = 0;
            var callbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var timer = CustomizedPingTimerFactory.CreateCustomizedPingTimer(loggerFactory.CreateLogger(
                nameof(StartStopDispose_StartDisposeStop)), nameof(StartStopDispose_StartDisposeStop),
                () =>
                {
                    // Hold the callback until the test stops the timer so another tick cannot race the assertion.
                    var releaseTask = releaseCallback.Task;
                    Interlocked.Increment(ref callbackCount);
                    callbackStarted.TrySetResult(true);
                    return releaseTask;
                },
                BaseTs, BaseTs);

            timer.Start();
            await callbackStarted.Task.OrTimeout(TimeSpan.FromSeconds(30));
            timer.Stop();
            releaseCallback.SetResult(true);
            Assert.Equal(1, callbackCount);
            await Task.Delay(BaseTsPlus * 5);
            timer.Dispose();
            Assert.Equal(1, callbackCount);

            callbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            releaseCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            timer.Start();
            await callbackStarted.Task.OrTimeout(TimeSpan.FromSeconds(30));
            timer.Dispose();
            releaseCallback.SetResult(true);
            Assert.Equal(2, callbackCount);
            await Task.Delay(BaseTsPlus * 5);
            timer.Stop();
            Assert.Equal(2, callbackCount);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task LongRunningCallback(int timerTicks)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
        {
            var callbackCount = 0;
            var firstCallbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondCallbackStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseSecondCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var timer = CustomizedPingTimerFactory.CreateCustomizedPingTimer(loggerFactory.CreateLogger(
                nameof(LongRunningCallback)), nameof(LongRunningCallback),
                async () =>
                {
                    if (Interlocked.Increment(ref callbackCount) == 1)
                    {
                        firstCallbackStarted.TrySetResult(true);
                        await releaseFirstCallback.Task;
                    }
                    else
                    {
                        secondCallbackStarted.TrySetResult(true);
                        await releaseSecondCallback.Task;
                    }
                },
                BaseTs, BaseTs);

            timer.Start();
            try
            {
                await firstCallbackStarted.Task.OrTimeout(TimeSpan.FromSeconds(30));
                // Keep the first callback running across multiple timer periods; callbacks must not overlap.
                await Task.Delay(BaseTsPlus * timerTicks);
                Assert.Equal(1, Volatile.Read(ref callbackCount));
                releaseFirstCallback.SetResult(true);

                await secondCallbackStarted.Task.OrTimeout(TimeSpan.FromSeconds(30));
                // Stop while the second callback is held so another tick cannot race the assertion.
                timer.Stop();
                releaseSecondCallback.SetResult(true);
                Assert.Equal(2, Volatile.Read(ref callbackCount));

                // extra check it really stopped
                await Task.Delay(BaseTsPlus * 2 * timerTicks);
                Assert.Equal(2, Volatile.Read(ref callbackCount));
            }
            finally
            {
                timer.Stop();
                releaseFirstCallback.TrySetResult(true);
                releaseSecondCallback.TrySetResult(true);
            }
        }
    }

    private static ServiceConnectionContainerBase.CustomizedPingTimer CreatePingTimer(ILoggerFactory loggerFactory, Action counter)
    {
        return CustomizedPingTimerFactory.CreateCustomizedPingTimer(loggerFactory.CreateLogger(
            nameof(BasicStartStopTest)), nameof(BasicStartStopTest),
            () =>
            {
                counter();
                return Task.CompletedTask;
            },
            BaseTs, BaseTs);
    }

    private sealed class CustomizedPingTimerFactory : ServiceConnectionContainerBase
    {
        public CustomizedPingTimerFactory(IServiceConnectionFactory serviceConnectionFactory, int minConnectionCount, HubServiceEndpoint endpoint, IReadOnlyList<IServiceConnection> initialConnections = null, ILogger logger = null, AckHandler ackHandler = null) : base(serviceConnectionFactory, minConnectionCount, endpoint, initialConnections, logger, ackHandler)
        {
        }

        internal static CustomizedPingTimer CreateCustomizedPingTimer(ILogger logger, string name, Func<Task> func, TimeSpan due, TimeSpan interval)
        {
            return new CustomizedPingTimer(logger, name, func, due, interval);
        }
    }
}
