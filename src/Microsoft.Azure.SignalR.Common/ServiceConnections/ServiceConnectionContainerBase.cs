// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceConnectionContainerBase : IServiceConnectionContainer, IServiceMessageHandler, IDisposable
    {
        private const int CheckWindow = 5;
        private static readonly TimeSpan CheckTimeSpan = TimeSpan.FromMinutes(10);

        // Give interval(5s) * 24 = 2min window for retry considering abnormal case.
        private const int MaxRetryRemoveSeverConnection = 24;

        private static readonly int MaxReconnectBackOffInternalInMilliseconds = 1000;
        // Give (interval * 3 + 1) delay when check value expire.
        private static readonly long DefaultServersPingTimeoutTicks = Stopwatch.Frequency * (Constants.Periods.DefaultServersPingInterval.Seconds * 3 + 1);
        private static readonly Tuple<string, long> DefaultServersTagContext = new Tuple<string, long>(string.Empty, 0);

        private static TimeSpan ReconnectInterval =>
            TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackOffInternalInMilliseconds));

        private readonly BackOffPolicy _backOffPolicy = new BackOffPolicy();

        private readonly object _lock = new object();

        private readonly object _statusLock = new object();

        private (int count, DateTime? last) _inactiveInfo;

        private readonly AckHandler _ackHandler;

        private readonly CustomizedPingTimer _statusPing;
        private readonly CustomizedPingTimer _serversPing;

        private volatile List<IServiceConnection> _fixedServiceConnections;

        private volatile ServiceConnectionStatus _status;

        // <serversTag, latestTimestamp>
        private volatile Tuple<string, long> _serversTagContext = DefaultServersTagContext;
        private volatile bool _hasClients;
        private volatile bool _enableMessageLog = false;
        private volatile bool _terminated = false;

        protected ILogger Logger { get; }

        protected List<IServiceConnection> FixedServiceConnections
        {
            get { return _fixedServiceConnections; }
            set { _fixedServiceConnections = value; }
        }

        protected IServiceConnectionFactory ServiceConnectionFactory { get; }

        protected int FixedConnectionCount { get; }

        protected virtual ServiceConnectionType InitialConnectionType { get; } = ServiceConnectionType.Default;

        public HubServiceEndpoint Endpoint { get; }

        public event Action<StatusChange> ConnectionStatusChanged;

        public string ServersTag => _serversTagContext.Item1;

        public bool HasClients => _hasClients;

        public ServiceConnectionStatus Status
        {
            get => _status;

            private set
            {
                if (_status != value)
                {
                    lock (_statusLock)
                    {
                        if (_status != value)
                        {
                            var prev = _status;
                            _status = value;
                            ConnectionStatusChanged?.Invoke(new StatusChange(prev, value));
                        }
                    }
                }
            }
        }

        protected ServiceConnectionContainerBase(IServiceConnectionFactory serviceConnectionFactory,
                                                 int minConnectionCount,
                                                 HubServiceEndpoint endpoint,
                                                 IReadOnlyList<IServiceConnection> initialConnections = null,
                                                 ILogger logger = null,
                                                 AckHandler ackHandler = null)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ServiceConnectionFactory = serviceConnectionFactory;
            Endpoint = endpoint;
            _ackHandler = ackHandler ?? new AckHandler();

            // make sure it is after _endpoint is set
            // init initial connections 
            List<IServiceConnection> initial;
            if (initialConnections == null)
            {
                initial = new List<IServiceConnection>();
            }
            else
            {
                initial = new List<IServiceConnection>(initialConnections);
                foreach (var conn in initial)
                {
                    conn.ConnectionStatusChanged += OnConnectionStatusChanged;
                }
            }

            var remainingCount = minConnectionCount - (initialConnections?.Count ?? 0);
            if (remainingCount > 0)
            {
                // if still not match or greater than minConnectionCount, create more
                var remaining = CreateFixedServiceConnection(remainingCount);
                initial.AddRange(remaining);
            }

            FixedServiceConnections = initial;
            FixedConnectionCount = initial.Count;
            ConnectionStatusChanged += OnStatusChanged;

            _statusPing = new CustomizedPingTimer(Logger, Constants.CustomizedPingTimer.ServiceStatus, WriteServiceStatusPingAsync, Constants.Periods.DefaultStatusPingInterval, Constants.Periods.DefaultStatusPingInterval);
            _statusPing.Start();

            _serversPing = new CustomizedPingTimer(Logger, Constants.CustomizedPingTimer.Servers, WriteServersPingAsync, Constants.Periods.DefaultServersPingInterval, Constants.Periods.DefaultServersPingInterval);
        }

        public Task StartAsync() => Task.WhenAll(FixedServiceConnections.Select(c => StartCoreAsync(c)));

        public virtual Task StopAsync()
        {
            _terminated = true;
            return Task.WhenAll(FixedServiceConnections.Select(c => c.StopAsync()));
        }

        /// <summary>
        /// Start and manage the whole connection lifetime
        /// </summary>
        /// <returns></returns>
        protected async Task StartCoreAsync(IServiceConnection connection, string target = null)
        {
            if (_terminated)
            {
                return;
            }

            try
            {
                await connection.StartAsync(target);
            }
            finally
            {
                await OnConnectionComplete(connection);
            }
        }

        public virtual Task HandlePingAsync(PingMessage pingMessage)
        {
            if (RuntimeServicePingMessage.TryGetStatus(pingMessage, out var status))
            {
                Log.ReceivedServiceStatusPing(Logger, status, Endpoint);
                _hasClients = status;
                Endpoint.IsActive = GetServiceStatus(status, CheckWindow, CheckTimeSpan);
            }
            else if (RuntimeServicePingMessage.TryGetServersTag(pingMessage, out var serversTag, out var updatedTime))
            {
                Log.ReceivedServersTagPing(Logger, Endpoint);
                if (updatedTime > _serversTagContext.Item2)
                {
                    _serversTagContext = Tuple.Create(serversTag, updatedTime);
                }
            }
            if (RuntimeServicePingMessage.TryGetMessageLogEnableFlag(pingMessage, out var enableMessageLog))
            {
                _enableMessageLog = enableMessageLog;
            }
            return Task.CompletedTask;
        }

        public void HandleAck(AckMessage ackMessage)
        {
            _ackHandler.TriggerAck(ackMessage.AckId, (AckStatus)ackMessage.Status);
        }

        public Task ConnectionInitializedTask => Task.WhenAll(from connection in FixedServiceConnections
                                                              select connection.ConnectionInitializedTask);

        public virtual Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_enableMessageLog && serviceMessage is IMessageWithTracingId msg)
            {
                // todo: msg.TracingId = TracingIdGenerator.Generate()
                msg.TracingId = "";
            }

            return WriteToRandomAvailableConnection(serviceMessage);
        }

        public async Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
        {
            if (!(serviceMessage is IAckableMessage ackableMessage))
            {
                throw new ArgumentException($"{nameof(serviceMessage)} is not {nameof(IAckableMessage)}");
            }

            var task = _ackHandler.CreateAck(out var id, cancellationToken);
            ackableMessage.AckId = id;

            await WriteToRandomAvailableConnection(serviceMessage);

            var status = await task;
            switch (status)
            {
                case AckStatus.Ok:
                    return true;
                case AckStatus.NotFound:
                    return false;
                case AckStatus.Timeout:
                    throw new TimeoutException($"Ack-able message {serviceMessage.GetType()} waiting for ack timed out.");
                default:
                    // should not be hit.
                    return false;
            }
        }

        public virtual Task OfflineAsync(GracefulShutdownMode mode)
        {
            return Task.WhenAll(FixedServiceConnections.Select(c => RemoveConnectionAsync(c, mode)));
        }

        public Task StartGetServersPing()
        {
            if (_serversPing.Start())
            {
                // reset old value when true start.
                _serversTagContext = DefaultServersTagContext;
            }
            return Task.CompletedTask;
        }

        public Task StopGetServersPing()
        {
            _serversPing.Stop();
            return Task.CompletedTask;
        }

        // Ready for scalable containers
        public void Dispose()
        {
            _statusPing.Dispose();
            _serversPing.Dispose();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Create a connection for a specific service connection type
        /// </summary>
        protected IServiceConnection CreateServiceConnectionCore(ServiceConnectionType type)
        {
            var connection = ServiceConnectionFactory.Create(Endpoint, this, type);

            connection.ConnectionStatusChanged += OnConnectionStatusChanged;
            return connection;
        }

        protected virtual async Task OnConnectionComplete(IServiceConnection serviceConnection)
        {
            if (serviceConnection == null)
            {
                throw new ArgumentNullException(nameof(serviceConnection));
            }

            serviceConnection.ConnectionStatusChanged -= OnConnectionStatusChanged;

            if (serviceConnection.Status == ServiceConnectionStatus.Connected)
            {
                return;
            }

            var index = FixedServiceConnections.IndexOf(serviceConnection);
            if (index != -1)
            {
                await RestartServiceConnectionCoreAsync(index);
            }
        }

        protected void ReplaceFixedConnections(int index, IServiceConnection serviceConnection)
        {
            lock (_lock)
            {
                var newImmutableConnections = FixedServiceConnections.ToList();
                newImmutableConnections[index] = serviceConnection;
                FixedServiceConnections = newImmutableConnections;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ackHandler.Dispose();
            }
        }

        protected virtual ServiceConnectionStatus GetStatus()
        {
            return FixedServiceConnections.Any(s => s.Status == ServiceConnectionStatus.Connected)
                ? ServiceConnectionStatus.Connected
                : ServiceConnectionStatus.Disconnected;
        }

        protected async Task WriteFinAsync(IServiceConnection c, GracefulShutdownMode mode)
        {
            var message = RuntimeServicePingMessage.GetFinPingMessage(mode);
            await c.WriteAsync(message);
        }

        protected async Task RemoveConnectionAsync(IServiceConnection c, GracefulShutdownMode mode)
        {
            var retry = 0;
            while (retry < MaxRetryRemoveSeverConnection)
            {
                using var source = new CancellationTokenSource();
                _ = WriteFinAsync(c, mode);

                var task = await Task.WhenAny(c.ConnectionOfflineTask, Task.Delay(Constants.Periods.RemoveFromServiceTimeout, source.Token));

                if (task == c.ConnectionOfflineTask)
                {
                    source.Cancel();
                    Log.ReceivedFinAckPing(Logger);
                    return;
                }
                retry++;
            }
            Log.TimeoutWaitingForFinAck(Logger, retry);
        }

        internal bool GetServiceStatus(bool active, int checkWindow, TimeSpan checkTimeSpan)
        {
            if (active)
            {
                _inactiveInfo = (0, null);
                return true;
            }
            else
            {
                var info = _inactiveInfo;
                var last = info.last ?? DateTime.UtcNow;
                var count = info.count;
                count++;
                _inactiveInfo = (count, last);

                // Inactive it only when it checks over 5 times and elapsed for over 10 minutes
                var inactive = count >= checkWindow && DateTime.UtcNow - last >= checkTimeSpan;
                return !inactive;
            }
        }

        internal static TimeSpan GetRetryDelay(int retryCount)
        {
            // retry count:   0, 1, 2, 3, 4,  5,  6,  ...
            // delay seconds: 1, 2, 4, 8, 16, 32, 60, ...
            if (retryCount > 5)
            {
                return TimeSpan.FromMinutes(1) + ReconnectInterval;
            }
            return TimeSpan.FromSeconds(1 << retryCount) + ReconnectInterval;
        }

        private void OnStatusChanged(StatusChange obj)
        {
            var online = obj.NewStatus == ServiceConnectionStatus.Connected;
            Endpoint.Online = online;
            if (!online)
            {
                Log.EndpointOffline(Logger, Endpoint);
            }
            else
            {
                Log.EndpointOnline(Logger, Endpoint);
            }
        }

        private void OnConnectionStatusChanged(StatusChange obj)
        {
            if (obj.NewStatus == ServiceConnectionStatus.Connected && Status != ServiceConnectionStatus.Connected)
            {
                Status = GetStatus();
            }
            else if (obj.NewStatus == ServiceConnectionStatus.Disconnected && Status != ServiceConnectionStatus.Disconnected)
            {
                Status = GetStatus();
            }
        }

        private async Task RestartServiceConnectionCoreAsync(int index)
        {
            Func<Task<bool>> tryNewConnection = async () =>
            {
                var connection = CreateServiceConnectionCore(InitialConnectionType);
                ReplaceFixedConnections(index, connection);

                _ = StartCoreAsync(connection);
                await connection.ConnectionInitializedTask;

                return connection.Status == ServiceConnectionStatus.Connected;
            };
            await _backOffPolicy.CallProbeWithBackOffAsync(tryNewConnection, GetRetryDelay);
        }

        private Task WriteToRandomAvailableConnection(ServiceMessage serviceMessage)
        {
            return WriteWithRetry(serviceMessage, StaticRandom.Next(-FixedConnectionCount, FixedConnectionCount), FixedConnectionCount);
        }

        private async Task WriteWithRetry(ServiceMessage serviceMessage, int initial, int count)
        {
            // go through all the connections, it can be useful when one of the remote service instances is down
            var maxRetry = count;
            var retry = 0;
            var index = (initial & int.MaxValue) % count;
            var direction = initial > 0 ? 1 : count - 1;
            while (retry < maxRetry)
            {
                var connection = FixedServiceConnections[index];
                if (connection != null && connection.Status == ServiceConnectionStatus.Connected)
                {
                    try
                    {
                        // still possible the connection is not valid
                        await connection.WriteAsync(serviceMessage);
                        return;
                    }
                    catch (ServiceConnectionNotActiveException)
                    {
                        if (retry == maxRetry - 1)
                        {
                            throw;
                        }
                    }
                }

                retry++;
                index = (index + direction) % count;
            }

            throw new ServiceConnectionNotActiveException();
        }

        private IEnumerable<IServiceConnection> CreateFixedServiceConnection(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return CreateServiceConnectionCore(InitialConnectionType);
            }
        }

        private async Task WriteServiceStatusPingAsync()
        {
            await WriteAsync(RuntimeServicePingMessage.GetStatusPingMessage(true));
        }

        private async Task WriteServersPingAsync()
        {
            if (Stopwatch.GetTimestamp() - _serversTagContext.Item2 > DefaultServersPingTimeoutTicks)
            {
                // reset value if expired.
                _serversTagContext = DefaultServersTagContext;
            }
            await WriteAsync(RuntimeServicePingMessage.GetServersPingMessage());
        }

        private sealed class CustomizedPingTimer : IDisposable
        {
            private readonly object _lock = new object();
            private readonly long _defaultPingTicks;

            private readonly string _pingName;
            private readonly Func<Task> _writePing;
            private readonly TimeSpan _dueTime;
            private readonly TimeSpan _intervalTime;
            private readonly ILogger _logger;

            // Considering parallel add endpoints to save time,
            // Add a counter control multiple time call Start() and Stop() correctly.
            private long _counter = 0;

            private long _lastSendTimestamp = 0;
            private TimerAwaitable _timer;

            public CustomizedPingTimer(ILogger logger, string pingName, Func<Task> writePing, TimeSpan dueTime, TimeSpan intervalTime)
            {
                _logger = logger;
                _pingName = pingName;
                _writePing = writePing;
                _dueTime = dueTime;
                _intervalTime = intervalTime;
                _defaultPingTicks = intervalTime.Seconds * Stopwatch.Frequency;

                _timer = Init();
            }

            public bool Start()
            {
                if (Interlocked.Increment(ref _counter) == 1)
                {
                    _timer.Start();
                    _ = PingAsync(_timer);
                    return true;
                }
                return false;
            }

            public void Stop()
            {
                // might be called by multi-thread, lock to ensure thread-safe for _counter update
                lock (_lock)
                {
                    if (Interlocked.Read(ref _counter) == 0)
                    {
                        // Avoid wrong Stop() to break _counter in further scale
                        Log.TimerAlreadyStopped(_logger, _pingName);
                        return;
                    }
                    if (Interlocked.Decrement(ref _counter) == 0)
                    {
                        _timer.Stop();
                    }
                }
            }

            public void Dispose()
            {
                 _timer.Stop();
            }

            private TimerAwaitable Init()
            {
                Log.StartingPingTimer(_logger, _pingName, _intervalTime);

                _lastSendTimestamp = Stopwatch.GetTimestamp();
                var timer = new TimerAwaitable(_dueTime, _intervalTime);

                return timer;
            }

            private async Task PingAsync(TimerAwaitable timer)
            {
                while (await timer)
                {
                    try
                    {
                        // Check if last send time is longer than default keep-alive ticks and then send ping
                        if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSendTimestamp) > _defaultPingTicks)
                        {
                            await _writePing.Invoke();

                            Interlocked.Exchange(ref _lastSendTimestamp, Stopwatch.GetTimestamp());
                            Log.SentPing(_logger, _pingName);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.FailedSendingPing(_logger, _pingName, e);
                    }
                }
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, string, Exception> _endpointOnline =
                LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, "EndpointOnline"), "Hub '{hub}' is now connected to '{endpoint}'.");

            private static readonly Action<ILogger, string, string, Exception> _endpointOffline =
                LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2, "EndpointOffline"), "Hub '{hub}' is now disconnected from '{endpoint}'. Please check log for detailed info.");

            private static readonly Action<ILogger, Exception> _receivedFinAckPing =
                LoggerMessage.Define(LogLevel.Information, new EventId(3, "ReceivedFinAckPing"), "Received FinAck ping.");

            private static readonly Action<ILogger, int, Exception> _timeoutWaitingForFinAck =
                LoggerMessage.Define<int>(LogLevel.Error, new EventId(4, "TimeoutWaitingForFinAck"), "Fail to receive FinAckPing after retry {retryCount} times.");

            private static readonly Action<ILogger, string, double, Exception> _startingPingTimer =
                LoggerMessage.Define<string, double>(LogLevel.Debug, new EventId(5, "StartingPingTimer"), "Starting { pingName } ping timer. Duration: {KeepAliveInterval:0.00}ms");

            private static readonly Action<ILogger, string, Exception> _sentPing =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(6, "SentPing"), "Sent a { pingName } ping message to service.");

            private static readonly Action<ILogger, string, Exception> _failedSendingPing =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(7, "FailedSendingPing"), "Failed sending a { pingName } ping message to service.");

            private static readonly Action<ILogger, bool, ServiceEndpoint, string, Exception> _receivedServiceStatusPing =
                LoggerMessage.Define<bool, ServiceEndpoint, string>(LogLevel.Debug, new EventId(8, "ReceivedServiceStatusPing"), "Received a service status active={isActive} from {endpoint} for hub {hub}.");

            private static readonly Action<ILogger, ServiceEndpoint, string, Exception> _receivedServersTagPing =
                LoggerMessage.Define<ServiceEndpoint, string>(LogLevel.Debug, new EventId(9, "ReceivedServersTagPing"), "Received a servers tag ping from {endpoint} for hub {hub}.");

            private static readonly Action<ILogger, string, Exception> _timerAlreadyStopped =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(10, "TimerAlreadyStopped"), "Failed to stop {pingName} timer as it's not started");

            public static void EndpointOnline(ILogger logger, HubServiceEndpoint endpoint)
            {
                _endpointOnline(logger, endpoint.Hub, endpoint.ToString(), null);
            }

            public static void EndpointOffline(ILogger logger, HubServiceEndpoint endpoint)
            {
                _endpointOffline(logger, endpoint.Hub, endpoint.ToString(), null);
            }

            public static void ReceivedFinAckPing(ILogger logger)
            {
                _receivedFinAckPing(logger, null);
            }

            public static void TimeoutWaitingForFinAck(ILogger logger, int retryCount)
            {
                _timeoutWaitingForFinAck(logger, retryCount, null);
            }

            public static void StartingPingTimer(ILogger logger, string pingName, TimeSpan keepAliveInterval)
            {
                _startingPingTimer(logger, pingName, keepAliveInterval.TotalMilliseconds, null);
            }

            public static void SentPing(ILogger logger, string pingName)
            {
                _sentPing(logger, pingName, null);
            }

            public static void FailedSendingPing(ILogger logger, string pingName, Exception exception)
            {
                _failedSendingPing(logger, pingName, exception);
            }

            public static void ReceivedServiceStatusPing(ILogger logger, bool isActive, HubServiceEndpoint endpoint)
            {
                _receivedServiceStatusPing(logger, isActive, endpoint, endpoint.Hub, null);
            }

            public static void ReceivedServersTagPing(ILogger logger, HubServiceEndpoint endpoint)
            {
                _receivedServersTagPing(logger, endpoint, endpoint.Hub, null);
            }

            public static void TimerAlreadyStopped(ILogger logger, string pingName)
            {
                _timerAlreadyStopped(logger, pingName, null);
            }
        }
    }
}
