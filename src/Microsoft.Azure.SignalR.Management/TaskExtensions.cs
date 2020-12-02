﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal static class TaskExtensions
    {
        private static readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);

        public static async Task OrTimeout(this Task task, CancellationToken cancellationToken)
        {
            if (cancellationToken == default)
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(_defaultTimeout))
                {
                    await task.OrTimeoutCore(cts.Token);
                }
            }
            else
            {
                await task.OrTimeoutCore(cancellationToken);
            }
        }

        private static async Task OrTimeoutCore(this Task task, CancellationToken cancellationToken)
        {
            if (task.IsCompleted)
            {
                await task;
                return;
            }

            await Task.WhenAny(task, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        }
    }
}
