// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceConnectionContainerBase : IServiceConnectionContainer, IServiceMessageHandler, IDisposable
    {
        private static readonly int MaxReconnectBackOffInternalInMilliseconds = 1000;
        private static TimeSpan ReconnectInterval =>
            TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackOffInternalInMilliseconds));

        private readonly BackOffPolicy _backOffPolicy = new BackOffPolicy();

        private readonly object _lock = new object();

        private readonly object _statusLock = new object();

        private readonly AckHandler _ackHandler;
        private readonly ILogger _logger;
        private volatile List<IServiceConnection> _fixedServiceConnections;

        private volatile ServiceConnectionStatus _status;

        protected List<IServiceConnection> FixedServiceConnections
        {
            get { return _fixedServiceConnections; }
            set { _fixedServiceConnections = value; }
        }

        protected IServiceConnectionFactory ServiceConnectionFactory { get; }

        protected int FixedConnectionCount { get; }

        protected virtual ServerConnectionType InitialConnectionType { get; } = ServerConnectionType.Default;

        public HubServiceEndpoint Endpoint { get; }

        public event Action<StatusChange> ConnectionStatusChanged;

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
            int minConnectionCount, HubServiceEndpoint endpoint,
            IReadOnlyList<IServiceConnection> initialConnections = null, ILogger logger = null)
        {
            ServiceConnectionFactory = serviceConnectionFactory;
            Endpoint = endpoint;
            _ackHandler = new AckHandler();

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
                foreach(var conn in initial)
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
            _logger = logger ?? NullLogger<ServiceConnectionBase>.Instance;
        }

        public Task StartAsync()
        {
            return Task.WhenAll(FixedServiceConnections.Select(c => StartCoreAsync(c)));
        }

        public virtual Task StopAsync() => Task.WhenAll(FixedServiceConnections.Select(c => c.StopAsync()));

        /// <summary>
        /// Start and manage the whole connection lifetime
        /// </summary>
        /// <returns></returns>
        protected async Task StartCoreAsync(IServiceConnection connection, string target = null)
        {
            try
            {
                await connection.StartAsync(target);
            }
            finally
            {
                await OnConnectionComplete(connection);
            }
        }

        public abstract Task HandlePingAsync(PingMessage pingMessage);

        public void HandleAck(AckMessage ackMessage)
        {
            if (ackMessage.Status == AckStatus.Ok)
            {
                _ackHandler.TriggerAck(ackMessage.AckId, true);
            }
            else
            {
                _ackHandler.TriggerAck(ackMessage.AckId, false);
            }
        }

        /// <summary>
        /// Create a connection for a specific service connection type
        /// </summary>
        protected IServiceConnection CreateServiceConnectionCore(ServerConnectionType type)
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
                Log.EndpointOffline(_logger, Endpoint);
            }
            else
            {
                Log.EndpointOnline(_logger, Endpoint);
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

            return await task;
        }

        // Ready for scalable containers
        public void Dispose()
        {
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

        private static class Log
        {
            private static readonly Action<ILogger, string, string, Exception> _endpointOnline =
                LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, "EndpointOnline"), "Hub '{hub}' is now connected to '{endpoint}'.");

            private static readonly Action<ILogger, string, string, Exception> _endpointOffline =
                LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(2, "EndpointOffline"), "Hub '{hub}' is now disconnected from '{endpoint}'. Please check log for detailed info.");

            public static void EndpointOnline(ILogger logger, HubServiceEndpoint endpoint)
            {
                _endpointOnline(logger, endpoint.Hub, endpoint.ToString(), null);
            }

            public static void EndpointOffline(ILogger logger, HubServiceEndpoint endpoint)
            {
                _endpointOffline(logger, endpoint.Hub, endpoint.ToString(), null);
            }
        }
    }
}
