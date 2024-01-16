// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class BackOffPolicyFacts : VerifiableLoggedTest
    {
        private static TimeSpan _overrunLeeway = TimeSpan.FromMilliseconds(250);

        private static TimeSpan _underrunLeeway = TimeSpan.FromMilliseconds(50);

        private static TimeSpan DefaultBackOff = TimeSpan.FromSeconds(2);

        private static Func<int, TimeSpan> DefaultBackOffFunc = (i) => DefaultBackOff;

        private static TimeSpan _1stBackOff = TimeSpan.FromSeconds(1.5);

        private static TimeSpan _2ndBackOff = TimeSpan.FromSeconds(2.5);

        private static TimeSpan _nxtBackOff = TimeSpan.FromSeconds(4.5);

        private TimeSpan _0s = TimeSpan.FromSeconds(0);

        private TimeSpan _1s = TimeSpan.FromSeconds(1);

        private TimeSpan _2s = TimeSpan.FromSeconds(2);

        private TimeSpan _10s = TimeSpan.FromSeconds(10);

        public BackOffPolicyFacts(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task AllProbesSuccessfulTest()
        {
            await RetryWhenExceptionThrows(async () => await RunProbeTests(new TestData()
            {
                Params = new ProbeParam[] {
                    new ProbeParam() {Result = true, Duration = _1s, Throws = false },
                    new ProbeParam() {Result = true, Duration = _0s, Throws = false },
                    new ProbeParam() {Result = true, Duration = _0s, Throws = false }
                },
                ExpectedCallTimes = new TimeSpan[] {
                    _0s,        // this gets called right away
                    _1s, _1s    // the following 2 will be called right after the duration of the first one
                },
                BkOffFunc = (i) => DefaultBackOff
            }));
        }

        [Fact]
        public async Task First2ProbesUnsuccessfulTest()
        {
            await RetryWhenExceptionThrows(async () => await RunProbeTests(new TestData()
            {
                Params = new ProbeParam[] {
                    new ProbeParam() {                              Result = false, Duration = _1s, Throws = false },
                    new ProbeParam() {                              Result = false, Duration = _1s, Throws = false },
                    new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _0s, Throws = false },
                    new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _0s, Throws = false },
                    new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _0s, Throws = false }
                },
                ExpectedCallTimes = new TimeSpan[] {
                    _0s,                            // these first 2 compete to get called right away,
                                                    // whoever wins returns false and induces 1st back off
                    _1stBackOff,                    // the 2nd one is called after the 1st failed back off,
                                                    // return false, induces the 2nd back off

                    _1stBackOff + _2ndBackOff,      // called after 1st + 2nd back offs, succeeds after 0 s
                    _1stBackOff + _2ndBackOff + _0s,// 1st + 2nd back offs + duration of 1st successful one (0 s)
                    _1stBackOff + _2ndBackOff + _0s // 1st + 2nd back offs + duration of 1st successful one (0 s)
                },
                BkOffFunc = (i) => i == 0 ? _1stBackOff : i == 1 ? _2ndBackOff : _nxtBackOff,
            }));
        }

        [Fact]
        public async Task FirstProbeThrowsTest()
        {
            await RetryWhenExceptionThrows(async () => await RunProbeTests(new TestData()
            {
                Params = new ProbeParam[] {
                    new ProbeParam() {                              Result = false, Duration = _0s, Throws=true },
                    new ProbeParam() {                              Result = false, Duration = _0s, Throws=true },
                    new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _1s, Throws=false },
                    new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _1s, Throws=false },
                    new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _1s, Throws=false }
                },
                ExpectedCallTimes = new TimeSpan[] {
                    _0s,                            // called right away, throws and induces first back off
                    _1stBackOff,                    // called after the 1st failed back off, throws, induces the 2nd back off
                    _1stBackOff + _2ndBackOff,      // called after 1st + 2nd back offs, succeeds after its duration (1s)
                    _1stBackOff + _2ndBackOff + _1s,// 1st + 2nd back offs + duration of 1st successful one (1s)
                    _1stBackOff + _2ndBackOff + _1s // 1st + 2nd back offs + duration of 1st successful one (1s)
                },
                BkOffFunc = (i) => i == 0 ? _1stBackOff : i == 1 ? _2ndBackOff : _nxtBackOff,
            }));
        }

        [Fact(Skip = "Flacky in CI")]
        public async Task FirstProbeTimeoutTest()
        {
            await RunProbeTests(new TestData()
            {
                Params = new ProbeParam[] {
                        new ProbeParam() {                              Result = false, Duration = _10s, Throws=false }, // t/o & fail
                        new ProbeParam() {                              Result = false, Duration = _10s, Throws=true },  // t/o & throw
                        new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _2s, Throws=false },
                        new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _2s, Throws=false },
                        new ProbeParam() {InitialDelay = _2ndBackOff,   Result = true, Duration = _2s, Throws=false }
                },

                ExpectedCallTimes = new TimeSpan[] {
                    _0s,                            // called right away, throws and induces first back off
                    _1stBackOff,                    // called after the 1st failed back off, throws, induces the 2nd back off
                    _1stBackOff + _2ndBackOff,      // called after 1st + 2nd back offs, succeeds after its duration (1s)
                    _1stBackOff + _2ndBackOff + _2s,// 1st + 2nd back offs + duration of 1st successful one (2s)
                    _1stBackOff + _2ndBackOff + _2s // 1st + 2nd back offs + duration of 1st successful one (2s)
                },
                BkOffFunc = (i) => i == 0 ? _1stBackOff : i == 1 ? _2ndBackOff : _nxtBackOff,
            });
        }

        private static async Task RunProbeTests(TestData testData)
        {
            // initialize a few things
            int _funcCallNumber = 0;
            var policy = new BackOffPolicy();
            testData.Results = new ProbeResult[testData.Params.Length];
            DateTime startTime = DateTime.UtcNow;
            Task[] probeTestTasks = new Task[testData.Params.Length];

            for (int i = 0; i < testData.Params.Length; i++)
            {
                int index = i;
                var currentProbe = testData.Params[index];
                testData.Results[index] = new ProbeResult();

                Func<Task<bool>> probeFunc = async () =>
                {
                    var result = testData.Results[index];
                    result.ActualCallTime = DateTime.UtcNow - startTime;
                    result.ActualCallOrder = Interlocked.Increment(ref _funcCallNumber);
                    Interlocked.Increment(ref result.ActualNumberOfCalls);

                    await Task.Delay(currentProbe.Duration);
                    if (currentProbe.Throws)
                    {
                        throw new Exception("exception from probe func");
                    }
                    return currentProbe.Result;
                };

                Func<Task> testFunc = async () =>
                {
                    try
                    {
                        // This delay effectively defines the order of the calls
                        await Task.Delay(currentProbe.InitialDelay);
                        testData.Results[index].ActualResult = await policy.CallProbeWithBackOffAsync(probeFunc, testData.BkOffFunc);
                    }
                    catch (Exception ex)
                    {
                        testData.Results[index].ActualException = ex;
                    }
                };

                probeTestTasks[index] = testFunc();
            }

            await Task.WhenAll(probeTestTasks);

            // verify
            for (int i = 0; i < testData.Params.Length; i++)
            {
                var param = testData.Params[i];
                var result = testData.Results[i];

                Assert.Equal(1, result.ActualNumberOfCalls);
                Assert.Equal(param.Result, result.ActualResult);
                Assert.NotEqual((result.ActualException == null), param.Throws);

                // knowing the actual order of the func call we can compare it with the expected time
                // ActualCallOrder starts with 1
                var expectedTime = testData.ExpectedCallTimes[result.ActualCallOrder - 1];

                Assert.False(
                    result.ActualCallTime < expectedTime - _underrunLeeway ||        // too early
                    result.ActualCallTime > expectedTime + _overrunLeeway);          // too late
            }
        }

        // To avoid very repetitive code we parametrize a generic test
        // with different inputs and different expNextIntected results
        public class ProbeParam
        {
            // delay before calling the probe (to control the order of calls)
            public TimeSpan InitialDelay = TimeSpan.Zero;

            public bool Result;

            public TimeSpan Duration;

            public bool Throws;
        }

        public class ProbeResult
        {
            public bool ActualResult;

            public Exception ActualException;

            public int ActualCallOrder;

            public int ActualNumberOfCalls;

            public TimeSpan ActualCallTime;
        }

        public class TestData
        {
            public ProbeParam[] Params;

            public Func<int, TimeSpan> BkOffFunc;

            public TimeSpan[] ExpectedCallTimes;

            public ProbeResult[] Results;
        }
    }
}
