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
    public class CustomizedTimerTests : VerifiableLoggedTest
    {
        private const int BasePeriodMs = 400;

        private static readonly TimeSpan BaseTs = TimeSpan.FromMilliseconds(BasePeriodMs);

        private static readonly TimeSpan BaseTsPlus = TimeSpan.FromMilliseconds(BasePeriodMs * 1.2); // +20% leeway to avoid false positives

        public CustomizedTimerTests(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
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
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX, "Flacky tests due to slow machine")]
        public async Task StartStopStartStop()
        {
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
        [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX, "Flacky tests due to slow machine")]
        public async Task StartStopDispose_StartDisposeStop()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
            {
                var callbackCount = 0;
                var timer = CreatePingTimer(loggerFactory, () => Interlocked.Increment(ref callbackCount));

                timer.Start();
                await Task.Delay(BaseTsPlus);
                timer.Stop();
                Assert.Equal(1, callbackCount);
                await Task.Delay(BaseTsPlus * 5);
                timer.Dispose();
                Assert.Equal(1, callbackCount);

                timer.Start();
                await Task.Delay(BaseTsPlus);
                timer.Dispose();
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

                using var timer = CustomizedPingTimerFactory.CreateCustomizedPingTimer(loggerFactory.CreateLogger(
                    nameof(BasicStartStopTest)), nameof(BasicStartStopTest),
                    async () =>
                    {
                        Interlocked.Increment(ref callbackCount);
                        // long running task to make timer skip timerTicks callbacks
                        await Task.Delay(BaseTsPlus * timerTicks);
                    },
                    BaseTs, BaseTs);

                timer.Start();
                await Task.Delay(BaseTsPlus * 2 * timerTicks);
                timer.Stop();
                Assert.Equal(2, callbackCount);

                // extra check it really stopped
                await Task.Delay(BaseTsPlus * 2 * timerTicks);
                Assert.Equal(2, callbackCount);
            }
        }

        private static ServiceConnectionContainerBase.CustomizedPingTimer CreatePingTimer(ILoggerFactory loggerFactory, Action counter) =>
            CustomizedPingTimerFactory.CreateCustomizedPingTimer(loggerFactory.CreateLogger(
                nameof(BasicStartStopTest)), nameof(BasicStartStopTest),
                () =>
                {
                    counter();
                    return Task.CompletedTask;
                },
                BaseTs, BaseTs);

        private class CustomizedPingTimerFactory : ServiceConnectionContainerBase
        {
            private CustomizedPingTimerFactory(IServiceConnectionFactory serviceConnectionFactory, int minConnectionCount, HubServiceEndpoint endpoint, IReadOnlyList<IServiceConnection> initialConnections = null, ILogger logger = null, AckHandler ackHandler = null) : base(serviceConnectionFactory, minConnectionCount, endpoint, initialConnections, logger, ackHandler)
            {
            }

            internal static CustomizedPingTimer CreateCustomizedPingTimer(ILogger logger, string name, Func<Task> func, TimeSpan due, TimeSpan interval) =>
                new CustomizedPingTimer(logger, name, func, due, interval);
        }
    }
}
