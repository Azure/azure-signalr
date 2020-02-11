// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Common.ServiceConnections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceConnectionContainer : IMultiEndpointServiceConnectionContainer
    {
        private readonly ConcurrentDictionary<ServiceEndpoint, IServiceConnectionContainer> _connectionContainers =
            new ConcurrentDictionary<ServiceEndpoint, IServiceConnectionContainer>();

        private readonly int _connectionCount;
        private readonly string _hubName;
        private readonly object _lock = new object();

        // TODO: change to load from serviceoptions after merge PR
        private readonly TimeSpan _scaleTimeout = TimeSpan.FromSeconds(300);
        private readonly int _scaleWaitIntervalInSeconds = Constants.DefaultGetServiceStatusIntervalInSeconds / 2 + 1;

        private readonly IMessageRouter _router;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IServiceConnectionFactory _serviceConnectionFactory;
        private readonly IServiceEndpointManager _serviceEndpointManager;

        private IReadOnlyList<HubServiceEndpoint> _endpoints;

        // Quick access for single endpoint.
        private IServiceConnectionContainer _inner;

        private bool _needRouter => _endpoints.Count > 1;

        public IReadOnlyDictionary<ServiceEndpoint, IServiceConnectionContainer> ConnectionContainers => _connectionContainers;

        internal MultiEndpointServiceConnectionContainer(
            string hub,
            Func<HubServiceEndpoint, IServiceConnectionContainer> generator,
            IServiceEndpointManager endpointManager,
            IMessageRouter router,
            ILoggerFactory loggerFactory,
            int scaleTimeoutInSeconds = 300
            )
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            _hubName = hub;
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<MultiEndpointServiceConnectionContainer>();
            _serviceEndpointManager = endpointManager; 
            _scaleTimeout = TimeSpan.FromSeconds(scaleTimeoutInSeconds);

            // provides a copy to the endpoint per container
            _endpoints = endpointManager.GetEndpoints(hub);

            // router is required for potential scale needs
            _router = router ?? throw new ArgumentNullException(nameof(router));
            foreach (var endpoint in _endpoints)
            {
                _connectionContainers[endpoint] = generator(endpoint);
            }
            // assign first element to _inner for quick access in single endpoint cases
            _inner = _connectionContainers.First().Value;

            _serviceEndpointManager.OnAdd += OnAdd;
            _serviceEndpointManager.OnRemove += OnRemove;
            _serviceEndpointManager.OnUpdate += OnUpdate;
        }

        public MultiEndpointServiceConnectionContainer(
            IServiceConnectionFactory serviceConnectionFactory,
            string hub,
            int count,
            IServiceEndpointManager endpointManager,
            IMessageRouter router,
            ILoggerFactory loggerFactory,
            int scaleTimeoutInSeconds = 300
            ) : this(
                hub,
                endpoint => CreateContainer(serviceConnectionFactory, endpoint, count, loggerFactory),
                endpointManager,
                router,
                loggerFactory,
                scaleTimeoutInSeconds
                )
        {
            // Preserve some properties for potential scale needs.
            _router = router;
            _connectionCount = count;
            _serviceConnectionFactory = serviceConnectionFactory;
        }

        public IEnumerable<ServiceEndpoint> GetOnlineEndpoints()
        {
            return _connectionContainers.Keys.Where(s => s.Online);
        }

        private static IServiceConnectionContainer CreateContainer(IServiceConnectionFactory serviceConnectionFactory, HubServiceEndpoint endpoint, int count, ILoggerFactory loggerFactory)
        {
            if (endpoint.EndpointType == EndpointType.Primary)
            {
                return new StrongServiceConnectionContainer(serviceConnectionFactory, count, endpoint, loggerFactory.CreateLogger<StrongServiceConnectionContainer>());
            }
            else
            {
                return new WeakServiceConnectionContainer(serviceConnectionFactory, count, endpoint, loggerFactory.CreateLogger<WeakServiceConnectionContainer>());
            }
        }

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public Task ConnectionInitializedTask
        {
            get
            {
                if (!_needRouter)
                {
                    return _inner.ConnectionInitializedTask;
                }

                return Task.WhenAll(from connection in _connectionContainers
                                    select connection.Value.ConnectionInitializedTask);
            }
        }

        public Task StartAsync()
        {
            if (!_needRouter)
            {
                return _inner.StartAsync();
            }

            return Task.WhenAll(_connectionContainers.Select(s =>
            {
                Log.StartingConnection(_logger, s.Key.Endpoint);
                return s.Value.StartAsync();
            }));
        }

        public Task StopAsync()
        {
            if (!_needRouter)
            {
                return _inner.StopAsync();
            }

            return Task.WhenAll(_connectionContainers.Select(s =>
            {
                Log.StoppingConnection(_logger, s.Key.Endpoint);
                return s.Value.StopAsync();
            }));
        }

        public Task OfflineAsync(bool migratable)
        {
            if (!_needRouter)
            {
                return _inner.OfflineAsync(migratable);
            }
            else
            {
                return Task.WhenAll(_connectionContainers.Select(c => c.Value.OfflineAsync(migratable)));
            }
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (!_needRouter)
            {
                return _inner.WriteAsync(serviceMessage);
            }
            return WriteMultiEndpointMessageAsync(serviceMessage, connection => connection.WriteAsync(serviceMessage));
        }

        public async Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
        {
            if (!_needRouter)
            {
                return await _inner.WriteAckableMessageAsync(serviceMessage, cancellationToken);
            }

            // If we have multiple endpoints, we should wait to one of the following conditions hit
            // 1. One endpoint responses "OK" state
            // 2. All the endpoints response failed state including "NotFound", "Timeout" and waiting response to timeout
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var writeMessageTask = WriteMultiEndpointMessageAsync(serviceMessage, async connection =>
            {
                var succeeded = await connection.WriteAckableMessageAsync(serviceMessage, cancellationToken);
                if (succeeded)
                {
                    tcs.TrySetResult(true);
                }
            });

            // If tcs.Task completes, one Endpoint responses "OK" state.
            var task = await Task.WhenAny(tcs.Task, writeMessageTask);

            // This will throw exceptions in tasks if exceptions exist
            await task;

            return tcs.Task.IsCompleted;
        }

        internal IEnumerable<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message)
        {
            var endpoints = _endpoints.ToList();
            switch (message)
            {
                case BroadcastDataMessage bdm:
                    return _router.GetEndpointsForBroadcast(endpoints);
                case GroupBroadcastDataMessage gbdm:
                    return _router.GetEndpointsForGroup(gbdm.GroupName, endpoints);
                case JoinGroupWithAckMessage jgm:
                    return _router.GetEndpointsForGroup(jgm.GroupName, endpoints);
                case LeaveGroupWithAckMessage lgm:
                    return _router.GetEndpointsForGroup(lgm.GroupName, endpoints);
                case MultiGroupBroadcastDataMessage mgbdm:
                    return mgbdm.GroupList.SelectMany(g => _router.GetEndpointsForGroup(g, endpoints)).Distinct();
                case ConnectionDataMessage cdm:
                    return _router.GetEndpointsForConnection(cdm.ConnectionId, endpoints);
                case MultiConnectionDataMessage mcd:
                    return mcd.ConnectionList.SelectMany(c => _router.GetEndpointsForConnection(c, endpoints)).Distinct();
                case UserDataMessage udm:
                    return _router.GetEndpointsForUser(udm.UserId, endpoints);
                case MultiUserDataMessage mudm:
                    return mudm.UserList.SelectMany(g => _router.GetEndpointsForUser(g, endpoints)).Distinct();
                default:
                    throw new NotSupportedException(message.GetType().Name);
            }
        }

        private Task WriteMultiEndpointMessageAsync(ServiceMessage serviceMessage, Func<IServiceConnectionContainer, Task> inner)
        {
            var routed = GetRoutedEndpoints(serviceMessage)?
                .Select(endpoint =>
                {
                    if (_connectionContainers.TryGetValue(endpoint, out var connection))
                    {
                        return (e: endpoint, c: connection);
                    }

                    Log.EndpointNotExists(_logger, endpoint.ToString());
                    return (e: endpoint, c: null);
                })
                .Where(c => c.c != null)
                .Select(async s =>
                {
                    try
                    {
                        await inner(s.c);
                    }
                    catch (ServiceConnectionNotActiveException)
                    {
                        // log and don't stop other endpoints
                        Log.FailedWritingMessageToEndpoint(_logger, serviceMessage.GetType().Name, s.e.ToString());
                    }
                }).ToArray();

            if (routed == null || routed.Length == 0)
            {
                // check if the router returns any endpoint
                Log.NoEndpointRouted(_logger, serviceMessage.GetType().Name);
                return Task.CompletedTask;
            }

            if (routed.Length == 1)
            {
                return routed[0];
            }

            return Task.WhenAll(routed);
        }

        private void OnAdd(HubServiceEndpoint endpoint)
        {
            if (!endpoint.Hub.Equals(_hubName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _ = AddHubServiceEndpointAsync(endpoint);
        }

        private void OnRemove(HubServiceEndpoint endpoint)
        {
            if (!endpoint.Hub.Equals(_hubName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            _ = RemoveHubServiceEndpointAsync(endpoint);
        }

        private void OnUpdate(HubServiceEndpoint endpoint)
        {
            if (!endpoint.Hub.Equals(_hubName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var oldEndpoint = _connectionContainers.Keys.Where(e => e.ConnectionString == endpoint.ConnectionString).FirstOrDefault();
            var oldHubEndpoint = _endpoints.Where(e => e.ConnectionString == endpoint.ConnectionString).FirstOrDefault();
            
            if (oldEndpoint != null)
            {
                _connectionContainers.TryRemove(oldEndpoint, out var container);
                _connectionContainers.TryAdd(endpoint, container);
                UpdateEndpointsStore(endpoint, ScaleOperation.Rename);
            }
            // update _inner as well
            _inner = _connectionContainers.FirstOrDefault().Value;
        }

        private async Task AddHubServiceEndpointAsync(HubServiceEndpoint endpoint)
        {
            if (_connectionContainers.ContainsKey(endpoint))
            {
                Log.EndpointAlreadyExists(_logger, endpoint.Endpoint);
                return;
            }

            try
            {
                var container = CreateContainer(_serviceConnectionFactory, endpoint, _connectionCount, _loggerFactory);
                _ = container.StartAsync();

                await WaitForServerStable(container, endpoint);

                // Enable message router after connection setup
                if (_connectionContainers.TryAdd(endpoint, container))
                {
                    UpdateEndpointsStore(endpoint, ScaleOperation.Add);
                    return;
                }
                Log.FailedAddingEndpointForMessageRouter(_logger, endpoint.Endpoint);
            }
            catch (Exception ex)
            {
                Log.FailedStartingConnectionForNewEndpoint(_logger, endpoint.Endpoint, ex);
            }
            finally
            {
                // Always make true finally to unblock ServiceEndpointManager
                endpoint.Ready = true;
            }
        }

        private async Task RemoveHubServiceEndpointAsync(HubServiceEndpoint endpoint)
        {
            if (_connectionContainers.TryGetValue(endpoint, out var container))
            {
                try
                {
                    // Notify ASRS to remove server connection from service router and disconnect clients
                    _ = container.OfflineAsync(false);
                    // wait for clients disconnect
                    await WaitForClientsDisconnect(container, endpoint);
                    // stop server connection
                    _ = container.StopAsync();
                }
                catch (Exception ex)
                {
                    Log.FailedRemovingConnectionForEndpoint(_logger, endpoint.Endpoint, ex);
                }
                finally
                {
                    // Clean message router cache and check _endpoints
                    if (!_connectionContainers.TryRemove(endpoint, out _))
                    {
                        Log.FailedRemovingEndpointForMessageRouter(_logger, endpoint.Endpoint);
                    }
                    UpdateEndpointsStore(endpoint, ScaleOperation.Remove);
                    // reset _inner
                    _inner = _connectionContainers.FirstOrDefault().Value;
                }
                return;
            }
            Log.EndpointNotExists(_logger, endpoint.Endpoint);
        }

        private void UpdateEndpointsStore(HubServiceEndpoint endpoint, ScaleOperation operation)
        { 
            lock(_lock)
            {
                var endpoints = _endpoints.ToList();
                switch (operation)
                {
                    case ScaleOperation.Add:
                        endpoints.Add(endpoint);
                        break;
                    case ScaleOperation.Remove:
                        endpoints.Remove(endpoint);
                        break;
                    case ScaleOperation.Rename:
                        endpoints = endpoints.Where(e => e.ConnectionString != endpoint.ConnectionString).ToList();
                        endpoints.Add(endpoint);
                        break;
                }
                _endpoints = endpoints;
            }
        }

        private async Task WaitForServerStable(IServiceConnectionContainer container, HubServiceEndpoint endpoint)
        {
            // TODO: start server ping: container.StartGetServersPing()
            // Check global servers are consistent among endpoints
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < _scaleTimeout)
            {
                if (IsServerStable(container))
                {
                    // TODO: stop servers ping if succeed: container.StopGetServersPing()
                    endpoint.Ready = true;
                    return;
                }
                // status ping interval is 10 seconds, delay to do next check
                await Task.Delay(_scaleWaitIntervalInSeconds * 1000);
            }
            // Finally make Ready after timeout
            endpoint.Ready = true;
            Log.TimeoutWaitingForAddingEndpoint(_logger, endpoint.Endpoint, _scaleTimeout.Seconds);
        }

        private bool IsServerStable(IServiceConnectionContainer container)
        {
            // var serversOnNew = container.GlobalServerIds;
            var allMatch = false; // serversOnNew > 0
            foreach (var connectionContainer in _connectionContainers)
            {
                // allMatch = serversOnNew.SetEquals(connectionContainer.Value.GlobalServerIds) && allMatch;
                if (!allMatch)
                {
                    return false;
                }
            }
            return allMatch;
        }

        private async Task WaitForClientsDisconnect(IServiceConnectionContainer container, ServiceEndpoint endpoint)
        {
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < _scaleTimeout)
            {
                //if (!container.HasCLient)
                //{
                //    return;
                //}
                // status ping interval is 10 seconds, delay to do next check
                await Task.Delay(_scaleWaitIntervalInSeconds * 1000);
            }
            Log.TimeoutWaitingClientsDisconnect(_logger, endpoint.Endpoint, _scaleTimeout.Seconds);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _startingConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Staring connections for endpoint {endpoint}.");

            private static readonly Action<ILogger, string, Exception> _stoppingConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "StoppingConnection"), "Stopping connections for endpoint {endpoint}.");

            private static readonly Action<ILogger, string, Exception> _endpointNotExists =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "EndpointNotExists"), "Endpoint {endpoint} from the router does not exists.");

            private static readonly Action<ILogger, string, Exception> _noEndpointRouted =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "NoEndpointRouted"), "Message {messageType} is not sent because no endpoint is returned from the endpoint router.");

            private static readonly Action<ILogger, string, string, Exception> _failedWritingMessageToEndpoint =
                LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(5, "FailedWritingMessageToEndpoint"), "Message {messageType} is not sent to endpoint {endpoint} because all connections to this endpoint are offline.");

            private static readonly Action<ILogger, string, Exception> _closingConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(6, "ClosingConnection"), "Closing connections for endpoint {endpoint}.");

            private static readonly Action<ILogger, string, Exception> _endpointAlreadyExists =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(7, "EndpointAlreadyExists"), "Endpoint {endpoint} already exists.");

            private static readonly Action<ILogger, string, Exception> _failedStartingConnectionForNewEndpoint =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(8, "FailedStartingConnectionForNewEndpoint"), "Fail to create and start server connection for new endpoint {endpoint}.");

            private static readonly Action<ILogger, string, Exception> _failedAddingEndpointForMessageRouter =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(9, "FailedAddingEndpointForMessageRouter"), "Fail to add endpoint {endpoint} for message router.");

            private static readonly Action<ILogger, string, Exception> _failedRemovingConnectionForEndpoint =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(10, "FailedRemovingConnectionForEndpoint"), "Fail to stop server connections for endpoint {endpoint}.");

            private static readonly Action<ILogger, string, Exception> _failedRemovingEndpointForMessageRouter =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(11, "FailedRemovingEndpointForMessageRouter"), "Fail to remove endpoint {endpoint} for message router.");

            private static readonly Action<ILogger, string, int, Exception> _timeoutWaitingForAddingEndpoint =
                LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(12, "TimeoutWaitingForAddingEndpoint"), "Timeout waiting for add a new endpoint {endpoint} in {timeoutSecond} seconds. Check if app configurations are consistant and restart app server.");

            private static readonly Action<ILogger, string, int, Exception> _timeoutWaitingClientsDisconnect =
                LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(13, "TimeoutWaitingClientsDisconnect"), "Timeout waiting for clients disconnect for {endpoint} in {timeoutSecond} seconds. Try restart app server to fix.");

            public static void StartingConnection(ILogger logger, string endpoint)
            {
                _startingConnection(logger, endpoint, null);
            }

            public static void StoppingConnection(ILogger logger, string endpoint)
            {
                _stoppingConnection(logger, endpoint, null);
            }

            public static void ClosingConnection(ILogger logger, string endpoint)
            {
                _closingConnection(logger, endpoint, null);
            }

            public static void EndpointNotExists(ILogger logger, string endpoint)
            {
                _endpointNotExists(logger, endpoint, null);
            }

            public static void NoEndpointRouted(ILogger logger, string messageType)
            {
                _noEndpointRouted(logger, messageType, null);
            }

            public static void FailedWritingMessageToEndpoint(ILogger logger, string messageType, string endpoint)
            {
                _failedWritingMessageToEndpoint(logger, messageType, endpoint, null);
            }

            public static void EndpointAlreadyExists(ILogger logger, string endpoint)
            {
                _endpointAlreadyExists(logger, endpoint, null);
            }

            public static void FailedStartingConnectionForNewEndpoint(ILogger logger, string endpoint, Exception ex)
            {
                _failedStartingConnectionForNewEndpoint(logger, endpoint, ex);
            }

            public static void FailedAddingEndpointForMessageRouter(ILogger logger, string endpoint)
            {
                _failedAddingEndpointForMessageRouter(logger, endpoint, null);
            }

            public static void FailedRemovingConnectionForEndpoint(ILogger logger, string endpoint, Exception ex)
            {
                _failedRemovingConnectionForEndpoint(logger, endpoint, ex);
            }

            public static void FailedRemovingEndpointForMessageRouter(ILogger logger, string endpoint)
            {
                _failedRemovingEndpointForMessageRouter(logger, endpoint, null);
            }

            public static void TimeoutWaitingForAddingEndpoint(ILogger logger, string endpoint, int timeoutSecond)
            {
                _timeoutWaitingForAddingEndpoint(logger, endpoint, timeoutSecond, null);
            }

            public static void TimeoutWaitingClientsDisconnect(ILogger logger, string endpoint, int timeoutSecond)
            {
                _timeoutWaitingClientsDisconnect(logger, endpoint, timeoutSecond, null);
            }
        }
    }
}
