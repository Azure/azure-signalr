#if !NETSTANDARD2_0
using System.Threading;

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    public sealed class ThreadStarvationDetector
    {
        public ILogger Logger { get; }

        public int Interval { get; }

        public int MaxStarvationWorkItemCount { get; }

        public ThreadStarvationDetector(ILogger<ThreadStarvationDetector> logger, int interval = 10000, int maxStarvationWorkItemCount = 200)
        {
            Logger = logger;
            Interval = interval;
            MaxStarvationWorkItemCount = maxStarvationWorkItemCount;
            new Thread(CheckThreadCount)
            {
                IsBackground = true
            }.Start();
        }

        private void CheckThreadCount(object obj)
        {
            int cooldown = 0;
            long lastCompletedWorkItemCount = 0;
            long lastPendingWorkItemCount = 0;
            while (true)
            {
                if (cooldown > 0)
                {
                    cooldown--;
                }
                var threadCount = ThreadPool.ThreadCount;
                var completedWorkItemCount = ThreadPool.CompletedWorkItemCount;
                var pendingWorkItemCount = ThreadPool.PendingWorkItemCount;
                ThreadPool.GetMaxThreads(out int maxWorkerThreads, out _);
                ThreadPool.GetAvailableThreads(out int availableWorkerThreads, out _);
                var busyThreads = maxWorkerThreads - availableWorkerThreads;
                var deltaWorkItemCount = completedWorkItemCount - lastCompletedWorkItemCount;
                Logger.LogWarning("Thread starving detected! ThreadCount:{0}, BusyThreads:{1}, DeltaWorkItemCount:{2}", threadCount, busyThreads, deltaWorkItemCount);

                bool isThreadStarving =
                    deltaWorkItemCount < lastPendingWorkItemCount &&
                    lastPendingWorkItemCount < pendingWorkItemCount &&
                    deltaWorkItemCount < MaxStarvationWorkItemCount;

                lastCompletedWorkItemCount = completedWorkItemCount;
                lastPendingWorkItemCount = pendingWorkItemCount;
                Thread.Sleep(Interval);
            }
        }
    }
}
#endif