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
        // Give interval(5s) * 24 = 2min window for retry considering abnormal case.
        private const int MaxRetryRemoveSeverConnection = 24;

        private static readonly int MaxReconnectBackOffInternalInMilliseconds = 1000;
        private static readonly TimeSpan RemoveFromServiceTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DefaultStatusPingInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultServersPingInterval = TimeSpan.FromSeconds(5);
        // Give (interval * 3 + 1) delay when check value expire.
        private static readonly long DefaultServersPingTimeoutTicks = Stopwatch.Frequency * (DefaultServersPingInterval.Seconds * 3 + 1);
        private static readonly Tuple<HashSet<string>, long> DefaultServerIdContext = new Tuple<HashSet<string>, long>(null, 0);

        private static readonly PingMessage _shutdownFinMessage = RuntimeServicePingMessage.GetFinPingMessage(false);
        private static readonly PingMessage _shutdownFinMigratableMessage = RuntimeServicePingMessage.GetFinPingMessage(true);

        private static TimeSpan ReconnectInterval =>
            TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackOffInternalInMilliseconds));

        private readonly BackOffPolicy _backOffPolicy = new BackOffPolicy();

        private readonly object _lock = new object();

        private readonly object _statusLock = new object();

        private readonly AckHandler _ackHandler;

        private readonly CustomizedPingTimer _statusPing;
        // Use Lazy for serversPing cause only apply for multiple endpoints scaling cases.
        private readonly Lazy<CustomizedPingTimer> _serversPing;

        private volatile List<IServiceConnection> _fixedServiceConnections;

        private volatile ServiceConnectionStatus _status;

        // <serverIds, lastServerIdsTimestamp>
        private volatile Tuple<HashSet<string>, long> _serverIdContext;
        private volatile bool _hasClients;
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

        public HashSet<string> GlobalServerIds => _serverIdContext.Item1;

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

            _statusPing = new CustomizedPingTimer(Logger, Constants.CustomizedPingTimer.ServiceStatus, WriteServiceStatusPingAsync, DefaultStatusPingInterval, DefaultStatusPingInterval);
            _statusPing.Start();

            _serversPing = new Lazy<CustomizedPingTimer>(() => CreateServersPingTimer());
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
            }
            else if (RuntimeServicePingMessage.TryGetServerIds(pingMessage, out var serverIds, out var updatedTime))
            {
                Log.ReceivedServerIdsPing(Logger, Endpoint);
                if (updatedTime > _serverIdContext.Item2)
                {
                    _serverIdContext = new Tuple<HashSet<string>,long>(serverIds, updatedTime);
                }
            }
            return Task.CompletedTask;
        }

        public void HandleAck(AckMessage ackMessage)
        {
            _ackHandler.TriggerAck(ackMessage.AckId, (AckStatus)ackMessage.Status);
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

        protected void ReplaceFixedConnections(int index, IServiceConnection serviceConnection)
        {
            lock (_lock)
            {
                var newImmutableConnections = FixedServiceConnections.ToList();
                newImmutableConnections[index] = serviceConnection;
                FixedServiceConnections = newImmutableConnections;
            }
        }

        public Task ConnectionInitializedTask => Task.WhenAll(from connection in FixedServiceConnections
                                                              select connection.ConnectionInitializedTask);

        public virtual Task WriteAsync(ServiceMessage serviceMessage)
        {
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

        public virtual Task OfflineAsync(bool migratable)
        {
            return Task.WhenAll(FixedServiceConnections.Select(c => RemoveConnectionAsync(c, migratable)));
        }

        public Task StartGetServersPing()
        {
            _serversPing.Value.Start();
            return Task.CompletedTask;
        }

        public Task StopGetServersPing()
        {
            // reset cached value
            _serverIdContext = DefaultServerIdContext;
            _serversPing.Value.Stop();
            return Task.CompletedTask;
        }

        // Ready for scalable containers
        public void Dispose()
        {
            _statusPing.Stop();
            // in case StopGetServersPingAsync is not executed.
            _serversPing.Value.Dispose();
            Dispose(true);
            GC.SuppressFinalize(this);
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

        protected async Task WriteFinAsync(IServiceConnection c, bool migratable)
        {
            if (migratable)
            {
                await c.WriteAsync(_shutdownFinMigratableMessage);
            }
            else
            {
                await c.WriteAsync(_shutdownFinMessage);
            }
        }

        protected async Task RemoveConnectionAsync(IServiceConnection c, bool migratable)
        {
            var retry = 0;
            while (retry < MaxRetryRemoveSeverConnection)
            {
                using var source = new CancellationTokenSource();
                _ = WriteFinAsync(c, migratable);

                var task = await Task.WhenAny(c.ConnectionOfflineTask, Task.Delay(RemoveFromServiceTimeout, source.Token));

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

        private async Task WriteServerIdsPingAsync()
        {
            await WriteAsync(RuntimeServicePingMessage.GetServersPingMessage());
            if (Stopwatch.GetTimestamp() - _serverIdContext.Item2 > DefaultServersPingTimeoutTicks)
            {
                // reset value if expired.
                _serverIdContext = DefaultServerIdContext;
            }
        }

        private CustomizedPingTimer CreateServersPingTimer()
        {
            return new CustomizedPingTimer(Logger, Constants.CustomizedPingTimer.Servers, WriteServerIdsPingAsync, DefaultServersPingInterval, DefaultServersPingInterval);
        }

        private sealed class CustomizedPingTimer : IDisposable
        {
            private long _lastSendTimestamp = 0;
            private readonly long _defaultPingTicks;

            private readonly string _pingName;
            private readonly Func<Task> _writePing;
            private readonly TimeSpan _dueTime;
            private readonly TimeSpan _intervalTime;

            // Considering parallel add endpoints to save time
            // Add a counter control multiple time call Start() and Stop() correctly
            private volatile int _counter = 0;

            private TimerAwaitable _timer;
            private ILogger _logger { get; }

            public CustomizedPingTimer(ILogger logger, string pingName, Func<Task> writePing, TimeSpan dueTime, TimeSpan intervalTime)
            {
                _logger = logger;
                _pingName = pingName;
                _writePing = writePing;
                _dueTime = dueTime;
                _intervalTime = intervalTime;
                _defaultPingTicks = intervalTime.Seconds * Stopwatch.Frequency;
            }

            public void Start()
            {
                if (_counter == 0)
                {
                    _timer = Init();
                    _timer.Start();
                    _ = PingAsync(_timer);
                }
                Interlocked.Increment(ref _counter);
            }

            public void Stop()
            {
                if (_counter == 0)
                {
                    Log.TimerAlreadyStopped(_logger, _pingName);
                    return;
                }

                if (_counter == 1)
                {
                    _timer.Stop();
                }
                Interlocked.Decrement(ref _counter);
            }

            public void Dispose()
            {
                if (_counter > 0)
                {
                    _timer.Stop();
                }
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
                using (timer)
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

            private static readonly Action<ILogger, ServiceEndpoint, string, Exception> _receivedServerIdsPing =
                LoggerMessage.Define<ServiceEndpoint, string>(LogLevel.Debug, new EventId(9, "ReceivedServerIdsPing"), "Received a server ids ping from {endpoint} for hub {hub}.");

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

            public static void ReceivedServerIdsPing(ILogger logger, HubServiceEndpoint endpoint)
            {
                _receivedServerIdsPing(logger, endpoint, endpoint.Hub, null);
            }

            public static void TimerAlreadyStopped(ILogger logger, string pingName)
            {
                _timerAlreadyStopped(logger, pingName, null);
            }
        }
    }
}
