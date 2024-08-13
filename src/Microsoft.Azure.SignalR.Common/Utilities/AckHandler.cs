using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

#nullable enable

namespace Microsoft.Azure.SignalR
{
    internal sealed class AckHandler : IDisposable
    {
        public static readonly AckHandler Singleton = new();
        private readonly ConcurrentDictionary<int, IAckInfo> _acks = new();
        private readonly Timer _timer;
        private readonly TimeSpan _defaultAckTimeout;
        private volatile bool _disposed;

        private int _nextId;
        private int NextId() => Interlocked.Increment(ref _nextId);

        public AckHandler(int ackIntervalInMilliseconds = 3000, int ackTtlInMilliseconds = 10000) : this(TimeSpan.FromMilliseconds(ackIntervalInMilliseconds), TimeSpan.FromMilliseconds(ackTtlInMilliseconds)) { }

        internal AckHandler(TimeSpan ackInterval, TimeSpan defaultAckTimeout)
        {
            _defaultAckTimeout = defaultAckTimeout;
            _timer = new Timer(_ => CheckAcks(), null, ackInterval, ackInterval);
        }

        public Task<AckStatus> CreateSingleAck(out int id, TimeSpan? ackTimeout = default, CancellationToken cancellationToken = default)
        {
            id = NextId();
            if (_disposed)
            {
                return Task.FromResult(AckStatus.Ok);
            }
            var info = (IAckInfo<AckStatus>)_acks.GetOrAdd(id, _ => new SingleAckInfo(ackTimeout ?? _defaultAckTimeout));
            if (info is MultiAckInfo)
            {
                throw new InvalidOperationException();
            }
            cancellationToken.Register(() => info.Cancel());
            return info.Task;
        }

        public static bool HandleAckStatus(IAckableMessage message, AckStatus status)
        {
            return status switch
            {
                AckStatus.Ok => true,
                AckStatus.NotFound => false,
                AckStatus.Timeout or AckStatus.InternalServerError => throw new TimeoutException($"Ack-able message {message.GetType()}(ackId: {message.AckId}) timed out."),
                _ => throw new AzureSignalRException($"Ack-able message {message.GetType()}(ackId: {message.AckId}) gets error ack status {status}."),
            };
        }

        public Task<AckStatus> CreateMultiAck(out int id, TimeSpan? ackTimeout = default)
        {
            id = NextId();
            if (_disposed)
            {
                return Task.FromResult(AckStatus.Ok);
            }
            var info = (IAckInfo<AckStatus>)_acks.GetOrAdd(id, _ => new MultiAckInfo(ackTimeout ?? _defaultAckTimeout));
            if (info is SingleAckInfo)
            {
                throw new InvalidOperationException();
            }
            return info.Task;
        }

        public void TriggerAck(int id, AckStatus status = AckStatus.Ok)
        {
            if (_acks.TryGetValue(id, out var info))
            {
                switch (info)
                {
                    case IAckInfo<AckStatus> ackInfo:
                        if (ackInfo.Ack(status))
                        {
                            _acks.TryRemove(id, out _);
                        }
                        break;
                    default:
                        throw new InvalidCastException($"Expected: IAckInfo<{typeof(IAckInfo<AckStatus>).Name}>, actual type: {info.GetType().Name}");
                }
            }
        }

        public void SetExpectedCount(int id, int expectedCount)
        {
            if (_disposed)
            {
                return;
            }

            if (_acks.TryGetValue(id, out var info))
            {
                if (info is not IMultiAckInfo multiAckInfo)
                {
                    throw new InvalidOperationException();
                }
                if (multiAckInfo.SetExpectedCount(expectedCount))
                {
                    _acks.TryRemove(id, out _);
                }
            }
        }

        private void CheckAcks()
        {
            if (_disposed)
            {
                return;
            }

            var utcNow = DateTime.UtcNow;

            foreach (var item in _acks)
            {
                var id = item.Key;
                var ack = item.Value;
                if (utcNow > ack.TimeoutAt)
                {
                    if (_acks.TryRemove(id, out _))
                    {
                        if (ack is SingleAckInfo singleAckInfo)
                        {
                            singleAckInfo.Ack(AckStatus.Timeout);
                        }
                        else if (ack is MultiAckInfo multipleAckInfo)
                        {
                            multipleAckInfo.ForceAck(AckStatus.Timeout);
                        }
                        else
                        {
                            ack.Cancel();
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;

            _timer.Dispose();

            while (!_acks.IsEmpty)
            {
                foreach (var item in _acks)
                {
                    var id = item.Key;
                    var ack = item.Value;
                    if (_acks.TryRemove(id, out _))
                    {
                        ack.Cancel();
                        if (ack is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }
            }
        }

        private interface IAckInfo
        {
            DateTime TimeoutAt { get; }
            void Cancel();
        }

        private interface IAckInfo<T> : IAckInfo
        {
            Task<T> Task { get; }
            bool Ack(T status);
        }

        public interface IMultiAckInfo
        {
            bool SetExpectedCount(int expectedCount);
        }

        private sealed class SingleAckInfo : IAckInfo<AckStatus>
        {
            public readonly TaskCompletionSource<AckStatus> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public DateTime TimeoutAt { get; }

            public SingleAckInfo(TimeSpan timeout)
            {
                TimeoutAt = DateTime.UtcNow + timeout;
            }

            public bool Ack(AckStatus status = AckStatus.Ok) =>
                _tcs.TrySetResult(status);

            public Task<AckStatus> Task => _tcs.Task;

            public void Cancel() => _tcs.TrySetCanceled();
        }

        private sealed class MultiAckInfo : IAckInfo<AckStatus>, IMultiAckInfo
        {
            public readonly TaskCompletionSource<AckStatus> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            private int _ackCount;
            private int? _expectedCount;

            public DateTime TimeoutAt { get; }

            public MultiAckInfo(TimeSpan timeout)
            {
                TimeoutAt = DateTime.UtcNow + timeout;
            }

            public bool SetExpectedCount(int expectedCount)
            {
                if (expectedCount < 0)
                {
                    throw new ArgumentException("Cannot less than 0.", nameof(expectedCount));
                }
                bool result;
                lock (_tcs)
                {
                    if (_expectedCount != null)
                    {
                        throw new InvalidOperationException("Cannot set expected count more than once!");
                    }
                    _expectedCount = expectedCount;
                    result = expectedCount <= _ackCount;
                }
                if (result)
                {
                    _tcs.TrySetResult(AckStatus.Ok);
                }
                return result;
            }

            public bool Ack(AckStatus status = AckStatus.Ok)
            {
                bool result;
                lock (_tcs)
                {
                    _ackCount++;
                    result = _expectedCount <= _ackCount;
                }
                if (result)
                {
                    _tcs.TrySetResult(status);
                }
                return result;
            }

            /// <summary>
            /// Forcely ack the multi ack regardless of the expected count.
            /// </summary>
            /// <param name="status"></param>
            /// <returns></returns>
            public bool ForceAck(AckStatus status = AckStatus.Ok)
            {
                lock (_tcs)
                {
                    _ackCount = _expectedCount ?? 0;
                }
                _tcs.TrySetResult(status);
                return true;
            }

            public Task<AckStatus> Task => _tcs.Task;

            public void Cancel() => _tcs.TrySetCanceled();
        }

    }
}
