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

namespace Microsoft.Azure.SignalR;

internal class MultiEndpointServiceConnectionContainer : IServiceConnectionContainer
{
    private readonly string _hubName;

    private readonly IMessageRouter _router;

    private readonly ILoggerFactory _loggerFactory;

    private readonly ILogger _logger;

    private readonly IServiceEndpointManager _serviceEndpointManager;

    private readonly TimeSpan _scaleTimeout;

    private readonly Func<HubServiceEndpoint, IServiceConnectionContainer> _generator;

    private readonly object _lock = new object();

    private (bool needRouter, IReadOnlyList<HubServiceEndpoint> endpoints) _routerEndpoints;

    private int _started = 0;

    public ServiceConnectionStatus Status => throw new NotSupportedException();

    public Task ConnectionInitializedTask
    {
        get
        {
            return Task.WhenAll(from connection in _routerEndpoints.endpoints
                                select connection.ConnectionContainer.ConnectionInitializedTask);
        }
    }

    public string ServersTag => throw new NotSupportedException();

    public bool HasClients => throw new NotSupportedException();

    public MultiEndpointServiceConnectionContainer(
        IServiceConnectionFactory serviceConnectionFactory,
        string hub,
        int count,
        int? maxCount,
        IServiceEndpointManager endpointManager,
        IMessageRouter router,
        ILoggerFactory loggerFactory,
        TimeSpan? scaleTimeout = null
        ) : this(
            hub,
            endpoint => CreateContainer(serviceConnectionFactory, endpoint, count, maxCount, loggerFactory),
            endpointManager,
            router,
            loggerFactory,
            scaleTimeout)
    {
    }

    internal MultiEndpointServiceConnectionContainer(
                                                string hub,
        Func<HubServiceEndpoint, IServiceConnectionContainer> generator,
        IServiceEndpointManager endpointManager,
        IMessageRouter router,
        ILoggerFactory loggerFactory,
        TimeSpan? scaleTimeout = null)
    {
        if (generator == null)
        {
            throw new ArgumentNullException(nameof(generator));
        }

        _hubName = hub;
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<MultiEndpointServiceConnectionContainer>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        _serviceEndpointManager = endpointManager;
        _scaleTimeout = scaleTimeout ?? Constants.Periods.DefaultScaleTimeout;

        // Reserve generator for potential scale use.
        _generator = generator;

        // provides a copy to the endpoint per container
        var endpoints = endpointManager.GetEndpoints(hub);
        UpdateRoutedEndpoints(endpoints);

        foreach (var endpoint in endpoints)
        {
            endpoint.ConnectionContainer = generator(endpoint);
        }

        _serviceEndpointManager.OnAdd += OnAdd;
        _serviceEndpointManager.OnRemove += OnRemove;
    }

    // for tests
    public IEnumerable<HubServiceEndpoint> GetOnlineEndpoints()
    {
        return _routerEndpoints.endpoints.Where(s => s.Online);
    }

    public Task StartAsync()
    {
        //ensure started only once
        return _started == 1 || Interlocked.CompareExchange(ref _started, 1, 0) == 1
            ? Task.CompletedTask
            : Task.WhenAll(_routerEndpoints.endpoints.Select(s =>
            {
                Log.StartingConnection(_logger, s.Endpoint);
                return s.ConnectionContainer.StartAsync();
            }));
    }

    public Task StopAsync()
    {
        return Task.WhenAll(_routerEndpoints.endpoints.Select(s =>
        {
            Log.StoppingConnection(_logger, s.Endpoint);
            return s.ConnectionContainer.StopAsync();
        }));
    }

    public Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token)
    {
        return Task.WhenAll(_routerEndpoints.endpoints.Select(c => c.ConnectionContainer.OfflineAsync(mode, token)));
    }

    public Task CloseClientConnections(CancellationToken token)
    {
        return Task.WhenAll(_routerEndpoints.endpoints.Select(c => c.ConnectionContainer.CloseClientConnections(token)));
    }

    public Task WriteAsync(ServiceMessage serviceMessage)
    {
        return CreateMessageWriter(serviceMessage).WriteAsync(serviceMessage);
    }

    public Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
    {
        return CreateMessageWriter(serviceMessage).WriteAckableMessageAsync(serviceMessage, cancellationToken);
    }

    public Task StartGetServersPing()
    {
        return Task.WhenAll(_routerEndpoints.endpoints.Select(c => c.ConnectionContainer.StartGetServersPing()));
    }

    public Task StopGetServersPing()
    {
        return Task.WhenAll(_routerEndpoints.endpoints.Select(c => c.ConnectionContainer.StopGetServersPing()));
    }

    public void Dispose()
    {
        foreach (var container in _routerEndpoints.endpoints)
        {
            container.ConnectionContainer.Dispose();
        }
    }

    internal IEnumerable<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message)
    {
        if (!_routerEndpoints.needRouter)
        {
            return _routerEndpoints.endpoints;
        }
        var endpoints = _routerEndpoints.endpoints;
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

            case UserJoinGroupMessage ujgm:
                return _router.GetEndpointsForGroup(ujgm.GroupName, endpoints).Intersect(_router.GetEndpointsForUser(ujgm.UserId, endpoints));

            case UserJoinGroupWithAckMessage ujgm:
                return _router.GetEndpointsForGroup(ujgm.GroupName, endpoints).Intersect(_router.GetEndpointsForUser(ujgm.UserId, endpoints));

            case UserLeaveGroupMessage ulgm:
                return _router.GetEndpointsForGroup(ulgm.GroupName, endpoints).Intersect(_router.GetEndpointsForUser(ulgm.UserId, endpoints));

            case UserLeaveGroupWithAckMessage ulgm:
                return _router.GetEndpointsForGroup(ulgm.GroupName, endpoints).Intersect(_router.GetEndpointsForUser(ulgm.UserId, endpoints));

            case CheckConnectionExistenceWithAckMessage checkConnectionMessage:
                return _router.GetEndpointsForConnection(checkConnectionMessage.ConnectionId, endpoints);

            case CheckUserExistenceWithAckMessage checkUserMessage:
                return _router.GetEndpointsForUser(checkUserMessage.UserId, endpoints);

            case CheckGroupExistenceWithAckMessage checkGroupMessage:
                return _router.GetEndpointsForGroup(checkGroupMessage.GroupName, endpoints);

            case CheckUserInGroupWithAckMessage checkUserInGroupMessage:
                return _router.GetEndpointsForGroup(checkUserInGroupMessage.GroupName, endpoints).Intersect(_router.GetEndpointsForUser(checkUserInGroupMessage.UserId, endpoints));

            case CloseConnectionMessage closeConnectionMessage:
                return _router.GetEndpointsForConnection(closeConnectionMessage.ConnectionId, endpoints);

            case ClientInvocationMessage clientInvocationMessage:
                return _router.GetEndpointsForConnection(clientInvocationMessage.ConnectionId, endpoints);

            case ServiceCompletionMessage serviceCompletionMessage:
                return _router.GetEndpointsForConnection(serviceCompletionMessage.ConnectionId, endpoints);

            // ServiceMappingMessage should never be sent to the service

            default:
                throw new NotSupportedException(message.GetType().Name);
        }
    }

    private static IServiceConnectionContainer CreateContainer(IServiceConnectionFactory serviceConnectionFactory, HubServiceEndpoint endpoint, int count, int? maxCount, ILoggerFactory loggerFactory)
    {
        if (endpoint.EndpointType == EndpointType.Primary)
        {
            return new StrongServiceConnectionContainer(serviceConnectionFactory, count, maxCount, endpoint, loggerFactory.CreateLogger<StrongServiceConnectionContainer>());
        }
        else
        {
            return new WeakServiceConnectionContainer(serviceConnectionFactory, count, endpoint, loggerFactory.CreateLogger<WeakServiceConnectionContainer>());
        }
    }

    private MultiEndpointMessageWriter CreateMessageWriter(ServiceMessage serviceMessage)
    {
        var targetEndpoints = GetRoutedEndpoints(serviceMessage)?.ToList();
        return new MultiEndpointMessageWriter(targetEndpoints, _loggerFactory);
    }

    private void OnAdd(HubServiceEndpoint endpoint)
    {
        if (!endpoint.Hub.Equals(_hubName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _ = AddHubServiceEndpointAsync(endpoint);
    }

    private async Task AddHubServiceEndpointAsync(HubServiceEndpoint endpoint)
    {
        if (endpoint.ScaleTask.IsCompleted)
        {
            UpdateEndpointsStore(endpoint, ScaleOperation.Add);
            return;
        }

        var container = _generator(endpoint);
        endpoint.ConnectionContainer = container;

        try
        {
            _ = container.StartAsync();

            await container.ConnectionInitializedTask;

            // Update local store directly after start connection
            // to get a uniformed action on trigger servers ping
            UpdateEndpointsStore(endpoint, ScaleOperation.Add);

            await StartGetServersPing();
            await WaitForServerStable(container, endpoint);
        }
        catch (Exception ex)
        {
            Log.FailedStartingConnectionForNewEndpoint(_logger, endpoint.ToString(), ex);
        }
        finally
        {
            _ = StopGetServersPing();
            endpoint.CompleteScale();
        }
    }

    private void OnRemove(HubServiceEndpoint endpoint)
    {
        if (!endpoint.Hub.Equals(_hubName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        _ = RemoveHubServiceEndpointAsync(endpoint);
    }

    private async Task RemoveHubServiceEndpointAsync(HubServiceEndpoint endpoint)
    {
        if (endpoint.ScaleTask.IsCompleted)
        {
            UpdateEndpointsStore(endpoint, ScaleOperation.Remove);
            return;
        }
        try
        {
            var container = _routerEndpoints.endpoints.FirstOrDefault(e => e.Endpoint == endpoint.Endpoint && e.EndpointType == endpoint.EndpointType);
            if (container == null)
            {
                Log.EndpointNotExists(_logger, endpoint.ToString());
                return;
            }

            // TDOO: shall we pass in cancellation token here?
            _ = container.ConnectionContainer.OfflineAsync(GracefulShutdownMode.Off, default);
            await WaitForClientsDisconnect(container);

            UpdateEndpointsStore(endpoint, ScaleOperation.Remove);

            // Clean up
            await container.ConnectionContainer.StopAsync();
            container.ConnectionContainer.Dispose();
        }
        catch (Exception ex)
        {
            Log.FailedRemovingConnectionForEndpoint(_logger, endpoint.ToString(), ex);
        }
        finally
        {
            endpoint.CompleteScale();
        }
    }

    private void UpdateEndpointsStore(HubServiceEndpoint endpoint, ScaleOperation operation)
    {
        // Use lock to ensure store update safety as parallel changes triggered in container side.
        lock (_lock)
        {
            switch (operation)
            {
                case ScaleOperation.Add:
                    {
                        var newEndpoints = _routerEndpoints.endpoints.ToList();
                        newEndpoints.Add(endpoint);
                        UpdateRoutedEndpoints(newEndpoints);
                        break;
                    }
                case ScaleOperation.Remove:
                    {
                        var newEndpoints = _routerEndpoints.endpoints.Where(e => !e.Equals(endpoint)).ToList();
                        UpdateRoutedEndpoints(newEndpoints);
                        break;
                    }
                default:
                    break;
            }
        }
    }

    private void UpdateRoutedEndpoints(IReadOnlyList<HubServiceEndpoint> currentEndpoints)
    {
        // router will be used when there's customized MessageRouter or multiple endpoints
        var needRouter = currentEndpoints.Count > 1 || !(_router is DefaultMessageRouter);
        _routerEndpoints = (needRouter, currentEndpoints);
    }

    private async Task WaitForServerStable(IServiceConnectionContainer container, HubServiceEndpoint endpoint)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < _scaleTimeout)
        {
            if (IsServerReady(container))
            {
                return;
            }
            await Task.Delay(Constants.Periods.DefaultServersPingInterval);
        }
        Log.TimeoutWaitingForAddingEndpoint(_logger, endpoint.ToString(), (int)_scaleTimeout.TotalSeconds);
    }

    private bool IsServerReady(IServiceConnectionContainer container)
    {
        var serversOnNew = container.ServersTag;
        var allMatch = !string.IsNullOrEmpty(serversOnNew);
        if (!allMatch)
        {
            // return directly if local server list is not set yet.
            return false;
        }

        // ensure strong consistency of server Ids for new endpoint towards exists
        foreach (var endpoint in _routerEndpoints.endpoints)
        {
            allMatch = !string.IsNullOrEmpty(endpoint.ConnectionContainer.ServersTag)
                && serversOnNew.Equals(endpoint.ConnectionContainer.ServersTag, StringComparison.OrdinalIgnoreCase)
                && allMatch;
            if (!allMatch)
            {
                return false;
            }
        }
        return allMatch;
    }

    private async Task WaitForClientsDisconnect(HubServiceEndpoint endpoint)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < _scaleTimeout)
        {
            if (!endpoint.ConnectionContainer.HasClients)
            {
                return;
            }
            // status ping interval is 10s, quick delay 5s to do next check
            await Task.Delay(Constants.Periods.DefaultCloseDelayInterval);
        }
        Log.TimeoutWaitingClientsDisconnect(_logger, endpoint.ToString(), (int)_scaleTimeout.TotalSeconds);
    }

    private IEnumerable<ServiceEndpoint> SingleOrNotSupported(IEnumerable<ServiceEndpoint> endpoints, ServiceMessage message)
    {
        var endpointCnt = endpoints.ToList().Count;
        if (endpointCnt == 1)
        {
            return endpoints;
        }
        if (endpointCnt == 0)
        {
            throw new ArgumentException("Client invocation is not sent because no endpoint is returned from the endpoint router.");
        }
        throw new NotSupportedException("Client invocation to wait for multiple endpoints' results is not supported yet.");
    }

    internal static class Log
    {
        private static readonly Action<ILogger, string, Exception> _startingConnection =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Staring connections for endpoint {endpoint}.");

        private static readonly Action<ILogger, string, Exception> _stoppingConnection =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "StoppingConnection"), "Stopping connections for endpoint {endpoint}.");

        private static readonly Action<ILogger, string, Exception> _endpointNotExists =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "EndpointNotExists"), "Endpoint {endpoint} from the router does not exists.");

        private static readonly Action<ILogger, string, Exception> _failedStartingConnectionForNewEndpoint =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(7, "FailedStartingConnectionForNewEndpoint"), "Fail to create and start server connection for new endpoint {endpoint}.");

        private static readonly Action<ILogger, string, int, Exception> _timeoutWaitingForAddingEndpoint =
            LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(8, "TimeoutWaitingForAddingEndpoint"), "Timeout waiting for add a new endpoint {endpoint} in {timeoutSecond} seconds. Check if app configurations are consistant and restart app server.");

        private static readonly Action<ILogger, string, int, Exception> _timeoutWaitingClientsDisconnect =
           LoggerMessage.Define<string, int>(LogLevel.Error, new EventId(9, "TimeoutWaitingClientsDisconnect"), "Timeout waiting for clients disconnect for {endpoint} in {timeoutSecond} seconds.");

        private static readonly Action<ILogger, string, Exception> _failedRemovingConnectionForEndpoint =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(10, "FailedRemovingConnectionForEndpoint"), "Fail to stop server connections for endpoint {endpoint}.");

        public static void StartingConnection(ILogger logger, string endpoint)
        {
            _startingConnection(logger, endpoint, null);
        }

        public static void StoppingConnection(ILogger logger, string endpoint)
        {
            _stoppingConnection(logger, endpoint, null);
        }

        public static void EndpointNotExists(ILogger logger, string endpoint)
        {
            _endpointNotExists(logger, endpoint, null);
        }

        public static void FailedStartingConnectionForNewEndpoint(ILogger logger, string endpoint, Exception ex)
        {
            _failedStartingConnectionForNewEndpoint(logger, endpoint, ex);
        }

        public static void TimeoutWaitingForAddingEndpoint(ILogger logger, string endpoint, int timeoutSecond)
        {
            _timeoutWaitingForAddingEndpoint(logger, endpoint, timeoutSecond, null);
        }

        public static void TimeoutWaitingClientsDisconnect(ILogger logger, string endpoint, int timeoutSecond)
        {
            _timeoutWaitingClientsDisconnect(logger, endpoint, timeoutSecond, null);
        }

        public static void FailedRemovingConnectionForEndpoint(ILogger logger, string endpoint, Exception ex)
        {
            _failedRemovingConnectionForEndpoint(logger, endpoint, ex);
        }
    }
}