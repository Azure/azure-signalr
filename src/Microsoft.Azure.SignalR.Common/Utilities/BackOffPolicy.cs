// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Common
{
    internal class BackOffPolicy
    {
        private TaskCompletionSource<bool> _currentProbeTcs = null;
        private int _currentRetryCount = 0;

        /// <summary>
        /// Provides a synchronized mechanism of calling probing funcs by multiple concurrent callers.
        /// Each caller's probe func will be invoked exactly one time.
        /// The probe call may get delayed depending on the result of previous probe calls.
        /// The delay is controlled by getRetryDelay func and the number of consecutive failed probe calls.
        /// </summary>
        /// <param name="probe"> this func returns a task with boolean result indicating if the probe was successful</param>
        /// <param name="getRetryDelay"> this func returns a TimeSpan delay for a given iteration number</param>
        /// <returns> 
        /// A task with its final state and result matching the state and result of the task returned by the probe func.
        /// This task will completes after either of the following happens:
        /// - probe's task changes state to task.IsCompletedSuccessfully == true and task.Result == true
        /// - probe's task.Result == false / faulted / cancelled and the delay defined by getRetryDelay has passed
        /// </returns>
        public async Task<bool> CallProbeWithBackOffAsync(Func<Task<bool>> probe, Func<int, TimeSpan> getRetryDelay)
        {
            bool calledProbeOnce = false;
            bool probeSuccess = false;
            var myTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            do
            {
                // ensure only one caller will be selected to call probing func
                var ongoingProbeTcs = Interlocked.CompareExchange(ref _currentProbeTcs, myTcs, null);
                if (ongoingProbeTcs == null)
                {
                    // initiate the probe and indicate its result to others
                    Task<bool> probeTask = null;
                    bool awaitProbeTask = false;
                    try
                    {
                        Debug.Assert(!calledProbeOnce);
                        calledProbeOnce = true;
                        probeTask = probe();

                        using (CancellationTokenSource delayCts = new CancellationTokenSource())
                        {
                            var delayTask = Task.Delay(getRetryDelay(_currentRetryCount++), delayCts.Token);
                            await Task.WhenAny(delayTask, probeTask);

                            // Handle success, timeout, and failure appropriately
                            if (//probeTask.IsCompletedSuccessfully in .NET Standard 2.1
                                probeTask.Status == TaskStatus.RanToCompletion && !probeTask.IsFaulted && !probeTask.IsCanceled
                                && probeTask.Result)
                            {
                                // probe is successful
                                delayCts.Cancel();
                                probeSuccess = true;
                            }
                            else if (delayTask.IsCompleted) //delayTask.IsCompletedSuccessfully
                            {
                                // probe timeout
                                // make sure we still await for the probe task after indicating the failure to others
                                awaitProbeTask = true;
                            }
                            else
                            {
                                // probe failed
                                // make sure we still await for the probe task 
                                awaitProbeTask = true;
                                // after waiting for the current backoff time and indicating failure to others
                                await delayTask;
                            }
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _currentProbeTcs, null);
                        // indicate the result to others
                        myTcs.SetResult(probeSuccess);
                    }

                    // take care of unfinished probe task
                    if (awaitProbeTask)
                    {
                        probeSuccess = await probeTask;
                    }
                }
                // wait for the shared probe's result and try ourselves in case of the shared probe success
                else if (await ongoingProbeTcs.Task)
                {
                    Debug.Assert(!calledProbeOnce);
                    calledProbeOnce = true;
                    probeSuccess = await probe();
                }
            }
            while (!calledProbeOnce);

            if (probeSuccess)
            {
                // each successful probe call resets the retry counter
                _currentRetryCount = 0;
            }
            return probeSuccess;
        }
    }
}
