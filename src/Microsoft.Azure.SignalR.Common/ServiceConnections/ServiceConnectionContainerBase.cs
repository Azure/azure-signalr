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
        private static readonly int MaxReconnectBackOffInternalInMilliseconds = 1000;
        private static readonly TimeSpan DefaultGetServiceStatusInterval = TimeSpan.FromSeconds(10);
        private static readonly long DefaultGetServiceStatusTicks = DefaultGetServiceStatusInterval.Seconds * Stopwatch.Frequency;
        private static readonly long DefaultGetServiceStatusTimeoutTicks = DefaultGetServiceStatusTicks * 3;
        private static TimeSpan ReconnectInterval =>
            TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackOffInternalInMilliseconds));

        private static TimeSpan RemoveFromServiceTimeout = TimeSpan.FromSeconds(3);

        private readonly BackOffPolicy _backOffPolicy = new BackOffPolicy();

        private readonly object _lock = new object();

        private readonly object _statusLock = new object();

        private readonly object _pingLock = new object();

        private readonly AckHandler _ackHandler;

        private readonly TimerAwaitable _timer;

        private volatile ServiceConnectionStatus _status;

        private volatile HashSet<string> _globalServerIds;

        private volatile bool _terminated = false;

        private TimerAwaitable _serverIdsTimer;

        private long _lastSendTimestamp = 0;

        private long _serverIdsLastUpdated = 0;

        private long _lastSendServerIdsTimestamp = 0;

        private static readonly PingMessage _shutdownFinMessage = RuntimeServicePingMessage.GetFinPingMessage();

        protected ILogger Logger { get; }

        protected List<IServiceConnection> FixedServiceConnections { get; set; }

        protected IServiceConnectionFactory ServiceConnectionFactory { get; }

        protected int FixedConnectionCount { get; }

        protected virtual ServiceConnectionType InitialConnectionType { get; } = ServiceConnectionType.Default;

        public HubServiceEndpoint Endpoint { get; }

        public event Action<StatusChange> ConnectionStatusChanged;

        public HashSet<string> GlobalServerIds 
        {
            get { return _globalServerIds; }
            private set { _globalServerIds = value; } 
        } 

        public bool HasClients { get; private set; }

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

            _timer = StartServiceStatusPingTimer();
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
                // Interlocked not support bool, use lock to ensure thread-safe
                lock (_pingLock)
                {
                    HasClients = status;
                }
            }
            else if (RuntimeServicePingMessage.TryGetServerIds(pingMessage, out var serverIds, out var updatedTime))
            {
                Log.ReceivedServerIdsPing(Logger, Endpoint);
                if (updatedTime > Interlocked.Read(ref _serverIdsLastUpdated))
                {
                    Interlocked.Exchange(ref _globalServerIds, serverIds);
                    Interlocked.Exchange(ref _serverIdsLastUpdated, updatedTime);
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

        public Task StartGetServersPingAsync()
        {
            _serverIdsTimer = StartServerIdsPingTimer();
            return Task.CompletedTask;
        }

        public Task StopGetServersPingAsync()
        {
            Interlocked.Exchange(ref _globalServerIds, null);
            _serverIdsTimer?.Stop();
            return Task.CompletedTask;
        }

        // Ready for scalable containers
        public void Dispose()
        {
            _timer.Stop();
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

        protected async Task WriteFinAsync(IServiceConnection c)
        {
            await c.WriteAsync(_shutdownFinMessage);
        }

        protected async Task RemoveConnectionAsync(IServiceConnection c)
        {
            _ = WriteFinAsync(c);

            using var source = new CancellationTokenSource();
            var task = await Task.WhenAny(c.ConnectionOfflineTask, Task.Delay(RemoveFromServiceTimeout, source.Token));
            source.Cancel();

            if (task != c.ConnectionOfflineTask)
            {
                // log
            }
        }

        public virtual Task OfflineAsync()
        {
            return Task.WhenAll(FixedServiceConnections.Select(c => RemoveConnectionAsync(c)));
        }

        private TimerAwaitable StartServiceStatusPingTimer()
        {
            Log.StartingServiceStatusPingTimer(Logger, DefaultGetServiceStatusInterval);

            _lastSendTimestamp = Stopwatch.GetTimestamp();
            var timer = new TimerAwaitable(DefaultGetServiceStatusInterval, DefaultGetServiceStatusInterval);
            _ = ServiceStatusPingAsync(timer);

            return timer;
        }

        private async Task ServiceStatusPingAsync(TimerAwaitable timer)
        {
            using (timer)
            {
                timer.Start();

                while (await timer)
                {
                    try
                    {
                        // Check if last send time is longer than default keep-alive ticks and then send ping
                        if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSendTimestamp) > DefaultGetServiceStatusTicks)
                        {
                            await WriteAsync(RuntimeServicePingMessage.GetStatusPingMessage(true));
                            
                            Interlocked.Exchange(ref _lastSendTimestamp, Stopwatch.GetTimestamp());
                            Log.SentServiceStatusPing(Logger);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.FailedSendingServiceStatusPing(Logger, e);
                    }
                }
            }
        }

        private TimerAwaitable StartServerIdsPingTimer()
        {
            Log.StartingServerIdsPingTimer(Logger, DefaultGetServiceStatusInterval);

            _lastSendServerIdsTimestamp = Stopwatch.GetTimestamp();
            var timer = new TimerAwaitable(DefaultGetServiceStatusInterval, DefaultGetServiceStatusInterval);
            _ = ServerIdsPingAsync(timer);

            return timer;
        }

        private async Task ServerIdsPingAsync(TimerAwaitable timer)
        {
            using (timer)
            {
                timer.Start();
        
                while (await timer)
                {
                    try
                    {
                        // Check if last send time is longer than default keep-alive ticks and then send ping
                        if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSendServerIdsTimestamp) > DefaultGetServiceStatusTicks)
                        {
                            await WriteAsync(RuntimeServicePingMessage.GetServersPingMessage());
                            if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _serverIdsLastUpdated) > DefaultGetServiceStatusTimeoutTicks)
                            {
                                // clear long term not updated values for accuracy
                                Interlocked.Exchange(ref _globalServerIds, null);
                            }
                            Interlocked.Exchange(ref _lastSendServerIdsTimestamp, Stopwatch.GetTimestamp());
                            Log.SentServerIdsPing(Logger);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.FailedSendingServerIdsPing(Logger, e);
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

            private static readonly Action<ILogger, double, Exception> _startingServiceStatusPingTimer =
                LoggerMessage.Define<double>(LogLevel.Debug, new EventId(5, "StartingServiceStatusPingTimer"), "Starting service status ping timer. Duration: {KeepAliveInterval:0.00}ms");

            private static readonly Action<ILogger, Exception> _sentServiceStatusPing =
                LoggerMessage.Define(LogLevel.Debug, new EventId(6, "SentServiceStatusPing"), "Sent a service status ping message to service.");

            private static readonly Action<ILogger, Exception> _failedSendingServiceStatusPing =
                LoggerMessage.Define(LogLevel.Warning, new EventId(7, "FailedSendingServiceStatusPing"), "Failed sending a service status ping message to service.");

            private static readonly Action<ILogger, bool, ServiceEndpoint, string, Exception> _receivedServiceStatusPing =
                LoggerMessage.Define<bool, ServiceEndpoint, string>(LogLevel.Debug, new EventId(8, "ReceivedServiceStatusPing"), "Received a service status active={isActive} from {endpoint} for hub {hub}.");


            private static readonly Action<ILogger, double, Exception> _startingServerIdsPingTimer =
                LoggerMessage.Define<double>(LogLevel.Debug, new EventId(9, "StartingServerIdsPingTimer"), "Starting get server ids ping timer. Duration: {KeepAliveInterval:0.00}ms");

            private static readonly Action<ILogger, Exception> _sentServerIdsPing =
                LoggerMessage.Define(LogLevel.Debug, new EventId(10, "SentServerIdsPing"), "Sent a get server ids ping message to service.");

            private static readonly Action<ILogger, Exception> _failedSendingServerIdsPing =
                LoggerMessage.Define(LogLevel.Warning, new EventId(11, "FailedSendingServerIdsPing"), "Failed sending a server ids ping message to service.");

            private static readonly Action<ILogger, ServiceEndpoint, string, Exception> _receivedServerIdsPing =
                LoggerMessage.Define<ServiceEndpoint, string>(LogLevel.Debug, new EventId(12, "ReceivedServerIdsPing"), "Received a server ids ping from {endpoint} for hub {hub}.");


            public static void EndpointOnline(ILogger logger, HubServiceEndpoint endpoint)
            {
                _endpointOnline(logger, endpoint.Hub, endpoint.ToString(), null);
            }

            public static void EndpointOffline(ILogger logger, HubServiceEndpoint endpoint)
            {
                _endpointOffline(logger, endpoint.Hub, endpoint.ToString(), null);
            }

            public static void StartingServiceStatusPingTimer(ILogger logger, TimeSpan keepAliveInterval)
            {
                _startingServiceStatusPingTimer(logger, keepAliveInterval.TotalMilliseconds, null);
            }

            public static void SentServiceStatusPing(ILogger logger)
            {
                _sentServiceStatusPing(logger, null);
            }

            public static void FailedSendingServiceStatusPing(ILogger logger, Exception exception)
            {
                _failedSendingServiceStatusPing(logger, exception);
            }

            public static void ReceivedServiceStatusPing(ILogger logger, bool isActive, HubServiceEndpoint endpoint)
            {
                _receivedServiceStatusPing(logger, isActive, endpoint, endpoint.Hub, null);
            }

            public static void StartingServerIdsPingTimer(ILogger logger, TimeSpan keepAliveInterval)
            {
                _startingServerIdsPingTimer(logger, keepAliveInterval.TotalMilliseconds, null);
            }

            public static void SentServerIdsPing(ILogger logger)
            {
                _sentServerIdsPing(logger, null);
            }

            public static void FailedSendingServerIdsPing(ILogger logger, Exception exception)
            {
                _failedSendingServerIdsPing(logger, exception);
            }

            public static void ReceivedServerIdsPing(ILogger logger, HubServiceEndpoint endpoint)
            {
                _receivedServerIdsPing(logger, endpoint, endpoint.Hub, null);
            }
        }
    }
}
