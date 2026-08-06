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

    [Fact]
    public async Task StartStopStartStop()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
        {
            using var callbacks = new ControlledCallbacks();
            using var timer = CustomizedPingTimerFactory.CreateCustomizedPingTimer(
                loggerFactory.CreateLogger(nameof(StartStopStartStop)),
                nameof(StartStopStartStop),
                callbacks.InvokeAsync,
                BaseTs,
                BaseTs);

            timer.Start();
            await callbacks.WaitForStartAsync();
            timer.Stop();
            await callbacks.CompleteAsync();
            Assert.Equal(1, callbacks.Count);

            timer.Start();
            await callbacks.WaitForStartAsync();
            timer.Stop();
            await callbacks.CompleteAsync();
            Assert.Equal(2, callbacks.Count);
            Assert.False(await callbacks.WaitForStartAsync(BaseTsPlus));
        }
    }

    [Fact]
    public async Task StartStopDispose_StartDisposeStop()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
        {
            using var callbacks = new ControlledCallbacks();
            using var timer = CustomizedPingTimerFactory.CreateCustomizedPingTimer(
                loggerFactory.CreateLogger(nameof(StartStopDispose_StartDisposeStop)),
                nameof(StartStopDispose_StartDisposeStop),
                callbacks.InvokeAsync,
                BaseTs,
                BaseTs);

            timer.Start();
            await callbacks.WaitForStartAsync();
            timer.Stop();
            await callbacks.CompleteAsync();
            Assert.Equal(1, callbacks.Count);
            timer.Dispose();

            timer.Start();
            await callbacks.WaitForStartAsync();
            timer.Dispose();
            await callbacks.CompleteAsync();
            timer.Stop();
            Assert.Equal(2, callbacks.Count);
            Assert.False(await callbacks.WaitForStartAsync(BaseTsPlus));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task LongRunningCallback(int timerTicks)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
        {
            using var callbacks = new ControlledCallbacks();

            using var timer = CustomizedPingTimerFactory.CreateCustomizedPingTimer(loggerFactory.CreateLogger(
                nameof(BasicStartStopTest)), nameof(BasicStartStopTest),
                callbacks.InvokeAsync,
                BaseTs, BaseTs);

            timer.Start();
            await callbacks.WaitForStartAsync();

            // Keep the first callback active across several timer ticks.
            await Task.Delay(BaseTsPlus * timerTicks);
            Assert.Equal(1, callbacks.Count);

            await callbacks.CompleteAsync();
            await callbacks.WaitForStartAsync();
            timer.Stop();
            await callbacks.CompleteAsync();

            Assert.Equal(2, callbacks.Count);
            Assert.False(await callbacks.WaitForStartAsync(BaseTsPlus));
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

    private sealed class ControlledCallbacks : IDisposable
    {
        private readonly SemaphoreSlim _completed = new(0);

        private readonly SemaphoreSlim _release = new(0);

        private readonly SemaphoreSlim _started = new(0);

        private int _count;

        public int Count => Volatile.Read(ref _count);

        public async Task CompleteAsync()
        {
            _release.Release();
            await _completed.WaitAsync().OrTimeout();
        }

        public void Dispose()
        {
            _completed.Dispose();
            _release.Dispose();
            _started.Dispose();
        }

        public async Task InvokeAsync()
        {
            Interlocked.Increment(ref _count);
            _started.Release();
            await _release.WaitAsync();
            _completed.Release();
        }

        public Task WaitForStartAsync()
        {
            return _started.WaitAsync().OrTimeout();
        }

        public Task<bool> WaitForStartAsync(TimeSpan timeout)
        {
            return _started.WaitAsync(timeout);
        }
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
