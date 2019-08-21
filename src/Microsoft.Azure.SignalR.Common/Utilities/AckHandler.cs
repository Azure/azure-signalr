using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal sealed class AckHandler : IDisposable
    {
        private readonly ConcurrentDictionary<int, AckInfo> _acks = new ConcurrentDictionary<int, AckInfo>();
        private readonly Timer _timer;
        private readonly TimeSpan _ackInterval;
        private readonly TimeSpan _ackTtl;
        private int _currentId = 0;

        public AckHandler(int ackIntervalInMilliseconds = 5000, int ackTtlInMilliseconds = 10000)
        {
            _ackInterval = TimeSpan.FromMilliseconds(ackIntervalInMilliseconds);
            _ackTtl = TimeSpan.FromMilliseconds(ackTtlInMilliseconds);

            bool restoreFlow = false;
            try
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                _timer = new Timer(state => ((AckHandler)state).CheckAcks(), state: this, dueTime: _ackInterval, period: _ackInterval);
            }
            finally
            {
                // Restore the current ExecutionContext
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }

        public Task<bool> CreateAck(out int id, CancellationToken cancellationToken = default)
        {
            id = Interlocked.Increment(ref _currentId);
            var tcs = _acks.GetOrAdd(id, _ => new AckInfo(_ackTtl)).Tcs;
            cancellationToken.Register(() => tcs.TrySetCanceled());
            return tcs.Task;
        }

        public void TriggerAck(int id, AckStatus ackStatus)
        {
            if (_acks.TryRemove(id, out var ack))
            {
                switch (ackStatus)
                {
                    case AckStatus.Ok:
                        ack.Tcs.TrySetResult(true);
                        break;
                    case AckStatus.NotFound:
                        ack.Tcs.TrySetResult(false);
                        break;
                    case AckStatus.Timeout:
                        ack.Tcs.TrySetCanceled();
                        break;
                }
            }
        }

        private void CheckAcks()
        {
            var utcNow = DateTime.UtcNow;

            foreach (var pair in _acks)
            {
                if (utcNow > pair.Value.Expired)
                {
                    if (_acks.TryRemove(pair.Key, out var ack))
                    {
                        ack.Tcs.TrySetCanceled();
                    }
                }
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();

            foreach (var pair in _acks)
            {
                if (_acks.TryRemove(pair.Key, out var ack))
                {
                    ack.Tcs.TrySetCanceled();
                }
            }
        }

        private class AckInfo
        {
            public TaskCompletionSource<bool> Tcs { get; private set; }

            public DateTime Expired { get; private set; }

            public AckInfo(TimeSpan ttl)
            {
                Expired = DateTime.UtcNow.Add(ttl);
                Tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }
}