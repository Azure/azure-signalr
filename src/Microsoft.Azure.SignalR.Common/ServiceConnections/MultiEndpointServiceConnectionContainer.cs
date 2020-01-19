// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
        private readonly int _connectionCount;
        private readonly IMessageRouter _router;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IServiceConnectionContainer _inner;
        private readonly IServiceConnectionFactory _serviceConnectionFactory;

        private List<HubServiceEndpoint> _hubEndpoints = new List<HubServiceEndpoint>();
        private bool _needRouter => _hubEndpoints.Count > 1;

        public Dictionary<ServiceEndpoint, IServiceConnectionContainer> Connections { get; } = new Dictionary<ServiceEndpoint, IServiceConnectionContainer>();

        internal MultiEndpointServiceConnectionContainer(
            string hub,
            Func<HubServiceEndpoint, IServiceConnectionContainer> generator,
            IServiceEndpointManager endpointManager,
            IMessageRouter router,
            ILoggerFactory loggerFactory)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            _logger = loggerFactory?.CreateLogger<MultiEndpointServiceConnectionContainer>() ?? throw new ArgumentNullException(nameof(loggerFactory));

            // provides a copy to the endpoint per container
            _hubEndpoints = endpointManager.GetEndpoints(hub).ToList();

            if (!_needRouter)
            {
                _inner = generator(_hubEndpoints[0]);
                Connections.Add(_hubEndpoints[0], _inner);
            }
            else
            {
                // router is required when endpoints > 1
                _router = router ?? throw new ArgumentNullException(nameof(router));
                Connections = _hubEndpoints.ToDictionary(s => (ServiceEndpoint)s, s => generator(s));
            }
        }

        public MultiEndpointServiceConnectionContainer(
            IServiceConnectionFactory serviceConnectionFactory,
            string hub,
            int count,
            IServiceEndpointManager endpointManager,
            IMessageRouter router,
            ILoggerFactory loggerFactory
            ) : this(
                hub,
                endpoint => CreateContainer(serviceConnectionFactory, endpoint, count, loggerFactory),
                endpointManager,
                router,
                loggerFactory
                )
        {
            // Preserve some information for potential scale needs.
            _router = router;
            _loggerFactory = loggerFactory;
            _connectionCount = count;
            _serviceConnectionFactory = serviceConnectionFactory;
        }

        public IEnumerable<ServiceEndpoint> GetOnlineEndpoints()
        {
            return Connections.Keys.Where(s => s.Online);
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

                return Task.WhenAll(from connection in Connections
                                    select connection.Value.ConnectionInitializedTask);
            }
        }

        public HashSet<string> GlobalServerIds => throw new NotSupportedException();

        public bool HasClients => throw new NotSupportedException();

        public Task StartAsync()
        {
            if (_inner != null)
            {
                return _inner.StartAsync();
            }

            return Task.WhenAll(Connections.Select(s =>
            {
                Log.StartingConnection(_logger, s.Key.Endpoint);
                return s.Value.StartAsync();
            }));
        }

        public Task StopAsync()
        {
            if (_inner != null)
            {
                return _inner.StopAsync();
            }

            return Task.WhenAll(Connections.Select(s =>
            {
                Log.StoppingConnection(_logger, s.Key.Endpoint);
                return s.Value.StopAsync();
            }));
        }

        public Task OfflineAsync()
        {
            if (_inner != null)
            {
                return _inner.OfflineAsync();
            }
            else
            {
                return Task.WhenAll(Connections.Select(c => c.Value.OfflineAsync()));
            }
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_inner != null)
            {
                return _inner.WriteAsync(serviceMessage);
            }
            return WriteMultiEndpointMessageAsync(serviceMessage, connection => connection.WriteAsync(serviceMessage));
        }

        public async Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
        {
            if (_inner != null)
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

        public async Task AddServiceEndpoint(HubServiceEndpoint endpoint, TimeSpan timeout)
        {
            if (Connections.ContainsKey(endpoint))
            {
                Log.EndpointAlreadyExists(_logger, endpoint.Endpoint);
                return;
            }
            try
            {
                var container = CreateContainer(_serviceConnectionFactory, endpoint, _connectionCount, _loggerFactory);
                await container.StartAsync();

                // Add to message router after connection setup
                _hubEndpoints.Add(endpoint);
                Connections.Add(endpoint, container);
                // wait for server stable then return to add endpoint to client negotation
                await WaitForServerStable(container, endpoint, timeout);
            }
            catch (Exception ex)
            {
                Log.FailedStartingConnectionForNewEndpoint(_logger, endpoint.Endpoint, ex);
            }
        }

        public async Task RemoveServiceEndpoint(HubServiceEndpoint endpoint, TimeSpan timeout)
        {
            if (Connections.TryGetValue(endpoint, out var container))
            {
                try
                {
                    // notify ASRS to remove server connection from service router and disconnect clients
                    _ = container.OfflineAsync();
                    // wait for clients disconnect
                    await WaitForClientsDisconnect(container, endpoint, timeout);
                    // stop server connection and remove endpoint from message router
                    await container.StopAsync();
                }
                catch (Exception ex)
                {
                    Log.FailedRemovingConnectionForEndpoint(_logger, endpoint.Endpoint, ex);
                }
                finally
                {
                    if (!(_hubEndpoints.Remove(endpoint) && Connections.Remove(endpoint)))
                    {
                        Log.FailedRemovingEndpointFromMessageRouter(_logger, endpoint.Endpoint);
                    }
                }
                return;
            }
            Log.EndpointNotExists(_logger, endpoint.Endpoint);
        }

        internal IEnumerable<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message)
        {
            var endpoints = _hubEndpoints;
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
                    if (Connections.TryGetValue(endpoint, out var connection))
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

        private async Task WaitForServerStable(IServiceConnectionContainer container, ServiceEndpoint endpoint, TimeSpan timeout)
        {
            // TODO: start server ping: container.StartGetServersPing()
            // and check global servers are consistent among endpoints
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < timeout)
            {
                var isStable = false;
                foreach (var connectionContainer in Connections)
                {
                    var serversOnNew = container.GlobalServerIds;
                    isStable = serversOnNew.Count > 0 && serversOnNew.SetEquals(connectionContainer.Value.GlobalServerIds);
                    // skip iteration if not stable and wait for next check.
                    if (!isStable)
                    {
                        break;
                    }
                }
                if (isStable)
                {
                    // TODO: stop servers ping if succeed: container.StopGetServersPing()
                    return;
                }
                // wait 3 seconds for next try
                await Task.Delay(3000);
            }
            Log.TimeoutWaitingForAddingEndpoint(_logger, endpoint.Endpoint, timeout.Minutes);
        }

        private async Task WaitForClientsDisconnect(IServiceConnectionContainer container, ServiceEndpoint endpoint, TimeSpan timeout)
        {
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < timeout)
            {
                if (!container.HasClients)
                {
                    return;
                }
                // wait 3 seconds for next try
                await Task.Delay(3000);
            }
            Log.TimeoutWaitingClientsDisconnect(_logger, endpoint.Endpoint, timeout.Minutes);
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

            private static readonly Action<ILogger, string, Exception> _failedRemovingConnectionForEndpoint =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(9, "FailedRemovingConnectionForEndpoint"), "Fail to stop server connections for endpoint {endpoint}.");
            
            private static readonly Action<ILogger, string, Exception> _failedRemovingEndpointFromMessageRouter =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(10, "FailedRemovingEndpointFromMessageRouter"), "Fail to remove endpoint {endpoint} from message router.");

            private static readonly Action<ILogger, string, int, Exception> _timeoutWaitingForAddingEndpoint =
                LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(11, "TimeoutWaitingForAddingEndpoint"), "Timeout waiting for add a new endpoint {endpoint} in {timeoutMinute} minutes. Check if app configurations are consistant and restart app server.");
            
            private static readonly Action<ILogger, string, int, Exception> _timeoutWaitingClientsDisconnect =
                LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(12, "TimeoutWaitingClientsDisconnect"), "Timeout waiting for clients disconnect for {endpoint} in {timeoutMinute} minutes. Try restart app server to fix.");

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

            public static void FailedRemovingConnectionForEndpoint(ILogger logger, string endpoint, Exception ex)
            {
                _failedRemovingConnectionForEndpoint(logger, endpoint, ex);
            }

            public static void FailedRemovingEndpointFromMessageRouter(ILogger logger, string endpoint)
            {
                _failedRemovingEndpointFromMessageRouter(logger, endpoint, null);
            }
            public static void TimeoutWaitingForAddingEndpoint(ILogger logger, string endpoint, int timeoutMinute)
            {
                _timeoutWaitingForAddingEndpoint(logger, endpoint, timeoutMinute, null);
            }

            public static void TimeoutWaitingClientsDisconnect(ILogger logger, string endpoint, int timeoutMinute)
            {
                _timeoutWaitingClientsDisconnect(logger, endpoint, timeoutMinute, null);
            }
        }
    }
}
