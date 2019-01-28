using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class AckHandler : IDisposable
    {
        private readonly ConcurrentDictionary<string, AckInfo> _acks = new ConcurrentDictionary<string, AckInfo>();
        private readonly Timer _timer;
        private readonly TimeSpan _ackThreshold = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _ackInterval = TimeSpan.FromSeconds(5);
        private readonly object _lock = new object();
        private bool _disposed;

        public AckHandler()
        {
            _timer = new Timer(_ => CheckAcks(), state: null, dueTime: _ackInterval, period: _ackInterval);
        }

        public Task CreateAck(string id)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return Task.CompletedTask;
                }

                return _acks.GetOrAdd(id, _ => new AckInfo()).Tcs.Task;
            }
        }

        public void TriggerAck(string id)
        {
            if (_acks.TryRemove(id, out var ack))
            {
                ack.Tcs.TrySetResult(null);
            }
        }

        private void CheckAcks()
        {
            if (_disposed)
            {
                return;
            }

            var utcNow = DateTime.UtcNow;

            foreach (var pair in _acks)
            {
                var elapsed = utcNow - pair.Value.Created;
                if (elapsed > _ackThreshold)
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
            lock (_lock)
            {
                _disposed = true;

                _timer.Dispose();

                foreach (var pair in _acks)
                {
                    if (_acks.TryRemove(pair.Key, out var ack))
                    {
                        ack.Tcs.TrySetCanceled();
                    }
                }
            }
        }

        private class AckInfo
        {
            public TaskCompletionSource<object> Tcs { get; private set; }
            public DateTime Created { get; private set; }

            public AckInfo()
            {
                Created = DateTime.UtcNow;
                Tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }


    }
}
