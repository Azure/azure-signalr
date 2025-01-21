// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
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
        public static readonly ServiceProtocol _serviceProtocol = new();
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
            var info = (IAckInfo<AckStatus>)_acks.GetOrAdd(id, _ => new SingleStatusAck(ackTimeout ?? _defaultAckTimeout));
            if (info is MultiAckWithStatusInfo)
            {
                throw new InvalidOperationException();
            }
            cancellationToken.Register(() => info.Cancel());
            return info.Task;
        }

        public Task<T> CreateSingleAck<T>(out int id, TimeSpan? ackTimeout = default, CancellationToken cancellationToken = default) where T : IMessagePackSerializable, new()
        {
            id = NextId();
            if (_disposed)
            {
                return Task.FromResult(new T());
            }
            var info = (IAckInfo<IMessagePackSerializable>)_acks.GetOrAdd(id, _ => new SinglePayloadAck<T>(ackTimeout ?? _defaultAckTimeout));
            cancellationToken.Register(info.Cancel);
            return info.Task.ContinueWith(task => (T)task.Result);
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
            var info = (IAckInfo<AckStatus>)_acks.GetOrAdd(id, _ => new MultiAckWithStatusInfo(ackTimeout ?? _defaultAckTimeout));
            if (info is SingleAckInfo<AckStatus>)
            {
                throw new InvalidOperationException();
            }
            return info.Task;
        }

        public void TriggerAck(int id, AckStatus status = AckStatus.Ok, ReadOnlySequence<byte>? payload = default)
        {
            if (_acks.TryGetValue(id, out var info) && info.Ack(status, payload))
            {
                _acks.TryRemove(id, out _);
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
                        if (ack is MultiAckWithStatusInfo multipleAckInfo)
                        {
                            multipleAckInfo.ForceAck(AckStatus.Timeout);
                        }
                        else
                        {
                            ack.Ack(AckStatus.Timeout);
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
            bool Ack(AckStatus status, ReadOnlySequence<byte>? payload = null);
        }

        private interface IAckInfo<T> : IAckInfo
        {
            Task<T> Task { get; }
        }

        public interface IMultiAckInfo
        {
            bool SetExpectedCount(int expectedCount);
        }

        private abstract class SingleAckInfo<T> : IAckInfo<T>
        {
            public readonly TaskCompletionSource<T> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public DateTime TimeoutAt { get; }
            public SingleAckInfo(TimeSpan timeout)
            {
                TimeoutAt = DateTime.UtcNow + timeout;
            }
            public abstract bool Ack(AckStatus status, ReadOnlySequence<byte>? payload = null);
            public Task<T> Task => _tcs.Task;
            public void Cancel() => _tcs.TrySetCanceled();
        }

        private class SingleStatusAck : SingleAckInfo<AckStatus>
        {

            public SingleStatusAck(TimeSpan timeout) : base(timeout) { }

            public override bool Ack(AckStatus status, ReadOnlySequence<byte>? payload = null) =>
                _tcs.TrySetResult(status);
        }

        private sealed class SinglePayloadAck<T> : SingleAckInfo<IMessagePackSerializable> where T : IMessagePackSerializable, new()
        {
            public SinglePayloadAck(TimeSpan timeout) : base(timeout) { }
            public override bool Ack(AckStatus status, ReadOnlySequence<byte>? payload = null)
            {
                if (status == AckStatus.Timeout)
                {
                    return _tcs.TrySetException(new TimeoutException($"Waiting for a {typeof(T).Name} response timed out."));
                }
                if (payload == null)
                {
                    return _tcs.TrySetException(new InvalidDataException($"The expected payload is null."));
                }

                try
                {
                    var result = _serviceProtocol.ParseMessagePayload<T>(payload.Value);
                    return _tcs.TrySetResult(result);
                }
                catch (Exception e)
                {
                    return _tcs.TrySetException(e);
                }
            }
        }

        private sealed class MultiAckWithStatusInfo : IAckInfo<AckStatus>, IMultiAckInfo
        {
            public readonly TaskCompletionSource<AckStatus> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            private int _ackCount;
            private int? _expectedCount;

            public DateTime TimeoutAt { get; }

            public MultiAckWithStatusInfo(TimeSpan timeout)
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

            public bool Ack(AckStatus status = AckStatus.Ok, ReadOnlySequence<byte>? payload = null)
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
