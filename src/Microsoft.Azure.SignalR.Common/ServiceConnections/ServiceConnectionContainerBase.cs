// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

internal abstract class ServiceConnectionContainerBase : IServiceConnectionContainer, IServiceMessageHandler
{
    private const int CheckWindow = 5;

    // Give interval(5s) * 24 = 2min window for retry considering abnormal case.
    private const int MaxRetryRemoveSeverConnection = 24;

    private static readonly TimeSpan CheckTimeSpan = TimeSpan.FromMinutes(10);

    private static readonly int MaxReconnectBackOffInternalInMilliseconds = 1000;

    private static readonly TimeSpan MessageWriteRetryDelay = TimeSpan.FromMilliseconds(200);

    private static readonly int MessageWriteMaxRetry = 3;

    // Give (interval * 3 + 1) delay when check value expire.
    private static readonly long DefaultServersPingTimeoutTicks = Stopwatch.Frequency * ((long)Constants.Periods.DefaultServersPingInterval.TotalSeconds * 3 + 1);

    private static readonly Tuple<string, long> DefaultServersTagContext = new Tuple<string, long>(string.Empty, 0);

    private readonly IReadOnlyDictionary<byte, StrongBox<WeakReference<IServiceConnection>>> _partitionedCache;

    private readonly BackOffPolicy _backOffPolicy = new BackOffPolicy();

    private readonly object _lock = new object();

    private readonly object _statusLock = new object();

    private readonly AckHandler _ackHandler;

    private readonly CustomizedPingTimer _statusPing;

    private readonly CustomizedPingTimer _serversPing;

    private readonly ServiceDiagnosticLogsContext _serviceDiagnosticLogsContext = new ServiceDiagnosticLogsContext { EnableMessageLog = false };

    private (int count, DateTime? last) _inactiveInfo;

    private volatile List<IServiceConnection> _serviceConnections;

    private volatile ServiceConnectionStatus _status;

    // <serversTag, latestTimestamp>
    private volatile Tuple<string, long> _serversTagContext = DefaultServersTagContext;

    private volatile bool _hasClients;

    private volatile bool _terminated = false;

    public HubServiceEndpoint Endpoint { get; }

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

    public Task ConnectionInitializedTask => Task.WhenAny(from connection in ServiceConnections
                                                          select connection.ConnectionInitializedTask);

    protected ILogger Logger { get; }

    protected List<IServiceConnection> ServiceConnections
    {
        get => _serviceConnections;
        set => _serviceConnections = value;
    }

    protected IServiceConnectionFactory ServiceConnectionFactory { get; }

    protected int FixedConnectionCount { get; }

    protected virtual ServiceConnectionType InitialConnectionType { get; } = ServiceConnectionType.Default;

    private static TimeSpan ReconnectInterval =>
                                                                                                                                                                                                TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackOffInternalInMilliseconds));

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

        // use globally unique AckHanlder if not specified
        // It is possible that the multiple MapHub calls the same hub, so that ack messages could be received by another instance of ServiceConnectionContainer
        // Use the ack handler singleton to allow ack message to be acked by another container instance
        _ackHandler = ackHandler ?? AckHandler.Singleton;

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

        ServiceConnections = initial;
        FixedConnectionCount = initial.Count;
        ConnectionStatusChanged += OnStatusChanged;

        _statusPing = new CustomizedPingTimer(Logger, Constants.CustomizedPingTimer.ServiceStatus, WriteServiceStatusPingAsync, Constants.Periods.DefaultStatusPingInterval, Constants.Periods.DefaultStatusPingInterval);

        // when server connection count is specified to 0, the app server only handle negotiate requests
        if (initial.Count > 0)
        {
            _ = StartStatusPing();
        }

        _serversPing = new CustomizedPingTimer(Logger, Constants.CustomizedPingTimer.Servers, WriteServersPingAsync, Constants.Periods.DefaultServersPingInterval, Constants.Periods.DefaultServersPingInterval);

        _partitionedCache = Enumerable.Range(0, 256).ToDictionary(i => (byte)i, i => new StrongBox<WeakReference<IServiceConnection>>(new WeakReference<IServiceConnection>(null)));
    }

    public event Action<StatusChange> ConnectionStatusChanged;

    public async Task StartAsync()
    {
        using (new ServiceConnectionContainerScope(_serviceDiagnosticLogsContext))
        {
            await Task.WhenAll(ServiceConnections.Select(c => StartCoreAsync(c)));
        }
    }

    public virtual Task StopAsync()
    {
        _terminated = true;
        _statusPing.Stop();
        return Task.WhenAll(ServiceConnections.Select(c => c.StopAsync()));
    }

    public virtual Task HandlePingAsync(PingMessage pingMessage)
    {
        if (RuntimeServicePingMessage.TryGetClientCount(pingMessage, out var clientCount))
        {
            Endpoint.EndpointMetrics.ClientConnectionCount = clientCount;
        }
        if (RuntimeServicePingMessage.TryGetServerCount(pingMessage, out var serverCount))
        {
            Endpoint.EndpointMetrics.ServerConnectionCount = serverCount;
        }
        if (RuntimeServicePingMessage.TryGetConnectionCapacity(pingMessage, out var connectionCapacity))
        {
            Endpoint.EndpointMetrics.ConnectionCapacity = connectionCapacity;
        }
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
        else if (RuntimeServicePingMessage.TryGetMessageLogEnableFlag(pingMessage, out var enableMessageLog))
        {
            _serviceDiagnosticLogsContext.EnableMessageLog = enableMessageLog;
        }
        return Task.CompletedTask;
    }

    public void HandleAck(AckMessage ackMessage)
    {
        _ackHandler.TriggerAck(ackMessage.AckId, (AckStatus)ackMessage.Status);
    }

    public virtual Task WriteAsync(ServiceMessage serviceMessage)
    {
        return WriteMessageAsync(serviceMessage);
    }

    public async Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
    {
        if (serviceMessage is not IAckableMessage ackableMessage)
        {
            throw new ArgumentException($"{nameof(serviceMessage)} is not {nameof(IAckableMessage)}");
        }

        var task = _ackHandler.CreateSingleAck(out var id, null, cancellationToken);
        ackableMessage.AckId = id;

        // Sending regular messages completes as soon as the data leaves the outbound pipe,
        // whereas ackable ones complete upon full roundtrip of the message and the ack (or timeout).
        // Therefore sending them over different connections creates a possibility for processing them out of original order.
        // By sending both message types over the same connection we ensure that they are sent (and processed) in their original order.
        await WriteMessageAsync(serviceMessage);

        var status = await task;
        return AckHandler.HandleAckStatus(ackableMessage, status);
    }

    public virtual Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token)
    {
        _terminated = true;
        return Task.WhenAll(ServiceConnections.Select(c => RemoveConnectionAsync(c, mode, token)));
    }

    public virtual Task CloseClientConnections(CancellationToken token)
    {
        return Task.WhenAll(ServiceConnections.Select(c => c.CloseClientConnections(token)));
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
        StopAsync().GetAwaiter().GetResult();
        _statusPing.Dispose();
        _serversPing.Dispose();
        Dispose(true);
        GC.SuppressFinalize(this);
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

    /// <summary>
    /// Create a connection for a specific service connection type
    /// </summary>
    protected IServiceConnection CreateServiceConnectionCore(ServiceConnectionType type)
    {
        var connection = ServiceConnectionFactory.Create(Endpoint, this, _ackHandler, type);

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

        var index = ServiceConnections.IndexOf(serviceConnection);
        if (index != -1)
        {
            // always try to restart first FixedConnectionCount connections
            if (index < FixedConnectionCount)
            {
                await RestartFixedServiceConnectionCoreAsync(index);
            }

            // the rest are "on demand" and are only created upon request
            else
            {
                RemoveOnDemandConnection(serviceConnection);
            }
        }
    }

    protected void AddOnDemandConnection(IServiceConnection serviceConnection)
    {
        lock (_lock)
        {
            var newImmutableConnections = ServiceConnections.ToList();
            newImmutableConnections.Add(serviceConnection);
            Debug.Assert(newImmutableConnections.IndexOf(serviceConnection) >= FixedConnectionCount);
            ServiceConnections = newImmutableConnections;
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
        return ServiceConnections.Any(s => s.Status == ServiceConnectionStatus.Connected)
            ? ServiceConnectionStatus.Connected
            : ServiceConnectionStatus.Disconnected;
    }

    protected async Task RemoveConnectionAsync(IServiceConnection c, GracefulShutdownMode mode, CancellationToken token)
    {
        var retry = 0;
        while (retry < MaxRetryRemoveSeverConnection && !token.IsCancellationRequested)
        {
            using var source = CancellationTokenSource.CreateLinkedTokenSource(token, new CancellationTokenSource(Constants.Periods.RemoveFromServiceTimeout).Token);

            var message = RuntimeServicePingMessage.GetFinPingMessage(mode);
            _ = c.WriteAsync(message);
            try
            {
                await c.ConnectionOfflineTask.OrCancelAsync(source.Token);
                source.Cancel();
                Log.ReceivedFinAckPing(Logger);
                return;
            }
            catch (OperationCanceledException)
            {
            }
            retry++;
        }
        Log.TimeoutWaitingForFinAck(Logger, retry);
    }

    private async Task StartStatusPing()
    {
        await this.ConnectionInitializedTask.ConfigureAwait(false);
        _statusPing.Start();
    }

    private async Task WriteMessageAsync(ServiceMessage serviceMessage)
    {
        var connection = SelectConnection(serviceMessage);

        var retry = 0;
        var maxRetry = MessageWriteMaxRetry;
        var delay = MessageWriteRetryDelay;
        while (true)
        {
            try
            {
                await connection.WriteAsync(serviceMessage);
                return;
            }
            catch (ServiceConnectionNotActiveException)
            {
                // enter the re-select logic
                retry++;
                if (retry == maxRetry)
                {
                    throw;
                }

                await Task.Delay(delay);
                connection = SelectConnection(serviceMessage);
            }
        }
    }

    private IServiceConnection SelectConnection(ServiceMessage message)
    {
        IServiceConnection connection = null;
        if (ClientConnectionScope.IsScopeEstablished)
        {
            // see if the execution context already has the connection stored for this container
            var containers = ClientConnectionScope.OutboundServiceConnections;
            if (!(containers.TryGetValue(Endpoint.UniqueIndex, out var connectionWeakReference)
                && connectionWeakReference.TryGetTarget(out connection)
                && IsActiveConnection(connection)))
            {
                connection = GetRandomActiveConnection();
                ClientConnectionScope.OutboundServiceConnections[Endpoint.UniqueIndex] = new WeakReference<IServiceConnection>(connection);
            }
        }
        else
        {
            // if it is not in scope
            // if message is partitionable, use the container's partition cache, otherwise use a random connection
            if (message is IPartitionableMessage partitionable)
            {
                var box = _partitionedCache[partitionable.PartitionKey];
                if (!box.Value.TryGetTarget(out connection) || !IsActiveConnection(connection))
                {
                    lock (box)
                    {
                        if (!box.Value.TryGetTarget(out connection) || !IsActiveConnection(connection))
                        {
                            connection = GetRandomActiveConnection();
                            box.Value.SetTarget(connection);
                        }
                    }
                }
            }
            else
            {
                connection = GetRandomActiveConnection();
            }
        }

        if (connection == null)
        {
            throw new ServiceConnectionNotActiveException();
        }

        return connection;
    }

    private bool IsActiveConnection(IServiceConnection connection)
    {
        return connection != null && connection.Status == ServiceConnectionStatus.Connected;
    }

    private IServiceConnection GetRandomActiveConnection()
    {
        var currentConnections = ServiceConnections;

        // go through all the connections, it can be useful when one of the remote service instances is down
        var count = currentConnections.Count;
        var initial = StaticRandom.Next(-count, count);
        var maxRetry = count;
        var retry = 0;
        var index = (initial & int.MaxValue) % count;
        var direction = initial > 0 ? 1 : count - 1;

        while (retry < maxRetry)
        {
            var connection = currentConnections[index];
            if (IsActiveConnection(connection))
            {
                return connection;
            }

            retry++;
            index = (index + direction) % count;
        }

        return null;
    }

    private async Task RestartFixedServiceConnectionCoreAsync(int index)
    {
        if (_terminated)
        {
            return;
        }

        Func<Task<bool>> tryNewConnection = async () =>
        {
            var connection = CreateServiceConnectionCore(InitialConnectionType);
            ReplaceFixedConnection(index, connection);

            _ = StartCoreAsync(connection);
            await connection.ConnectionInitializedTask;

            return connection.Status == ServiceConnectionStatus.Connected;
        };
        await _backOffPolicy.CallProbeWithBackOffAsync(tryNewConnection, GetRetryDelay);
    }

    private void ReplaceFixedConnection(int index, IServiceConnection serviceConnection)
    {
        lock (_lock)
        {
            var newImmutableConnections = ServiceConnections.ToList();
            newImmutableConnections[index] = serviceConnection;
            ServiceConnections = newImmutableConnections;
        }
    }

    private void RemoveOnDemandConnection(IServiceConnection serviceConnection)
    {
        lock (_lock)
        {
            var newImmutableConnections = ServiceConnections.ToList();
            Debug.Assert(newImmutableConnections.IndexOf(serviceConnection) >= FixedConnectionCount);
            newImmutableConnections.Remove(serviceConnection);
            ServiceConnections = newImmutableConnections;
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

    private IEnumerable<IServiceConnection> CreateFixedServiceConnection(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return CreateServiceConnectionCore(InitialConnectionType);
        }
    }

    private Task WriteServiceStatusPingAsync()
    {
        return SafeWriteAsync(RuntimeServicePingMessage.GetStatusPingMessage(true));
    }

    private Task WriteServersPingAsync()
    {
        if (Stopwatch.GetTimestamp() - _serversTagContext.Item2 > DefaultServersPingTimeoutTicks)
        {
            // reset value if expired.
            _serversTagContext = DefaultServersTagContext;
        }

        return SafeWriteAsync(RuntimeServicePingMessage.GetServersPingMessage());
    }

    private async Task SafeWriteAsync(ServiceMessage serviceMessage)
    {
        try
        {
            await WriteAsync(serviceMessage);
        }
        catch
        {
        }
    }

    protected internal sealed class CustomizedPingTimer : IDisposable
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
        }

        public bool Start()
        {
            if (Interlocked.Increment(ref _counter) == 1)
            {
                _timer = Init();
                _timer.Start();
                _ = PingAsync(_timer);
                return true;
            }
            return false;
        }

        public void Stop()
        {
            // might be called by multi-thread, lock to ensure thread-safety for _counter update
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
                    CleanupTimer();
                }
            }
        }

        public void Dispose()
        {
            CleanupTimer();
        }

        private void CleanupTimer()
        {
            var timer = Interlocked.Exchange(ref _timer, null);
            timer?.Stop();
            (timer as IDisposable)?.Dispose();
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
            LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(2, "EndpointOffline"), "Hub '{hub}' is now disconnected from '{endpoint}'. Please check log for detailed info.");

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
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(10, "TimerAlreadyStopped"), "Failed to stop {pingName} timer as it's not started");

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
