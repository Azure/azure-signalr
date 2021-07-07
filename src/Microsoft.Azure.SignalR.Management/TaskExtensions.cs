// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal static class TaskExtensions
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

        public static async Task OrTimeout(this Task task, CancellationToken cancellationToken, TimeSpan timeout = default, string taskDescription = "task")
        {
            timeout = timeout == default ? DefaultTimeout : timeout;
            var taskToCancel = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(task, taskToCancel);
            if (completed == taskToCancel && !task.IsCompleted)
            {
                if (taskToCancel.IsCanceled)
                {
                    throw new TaskCanceledException($"{taskDescription} is canceled.");
                }
                else
                {
                    throw new TimeoutException($"Timeout occurred for {taskDescription} after {timeout}.");
                }
            }
        }
    }
}