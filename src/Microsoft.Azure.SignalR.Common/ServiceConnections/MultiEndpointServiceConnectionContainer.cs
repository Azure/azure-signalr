// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Common.ServiceConnections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        private readonly IMessageRouter _router;
        private readonly ILogger _logger;
        private readonly IServiceConnectionContainer _inner;

        private IReadOnlyList<ServiceEndpoint> _endpoints;

        public Dictionary<ServiceEndpoint, IServiceConnectionContainer> Connections { get; }

        public MultiEndpointServiceConnectionContainer(Func<ServiceEndpoint, IServiceConnectionContainer> generator, IServiceEndpointManager endpointManager, IMessageRouter router, ILoggerFactory loggerFactory)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            _logger = loggerFactory?.CreateLogger<MultiEndpointServiceConnectionContainer>() ?? NullLogger<MultiEndpointServiceConnectionContainer>.Instance;

            // provides a copy to the endpoint per container
            _endpoints = endpointManager.Endpoints.Select(s => new ServiceEndpoint(s)).ToArray();

            if (_endpoints.Count == 1)
            {
                _inner = generator(_endpoints[0]);
            }
            else
            {
                // router is required when endpoints > 1
                _router = router ?? throw new ArgumentNullException(nameof(router));
                Connections = _endpoints.ToDictionary(s => s, s => generator(s));
            }
        }

        public MultiEndpointServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory, string hub,
            int count, IServiceEndpointManager endpointManager, IMessageRouter router, IServerNameProvider nameProvider, ILoggerFactory loggerFactory)
        :this(endpoint => CreateContainer(serviceConnectionFactory, endpoint, hub, count, endpointManager, nameProvider, loggerFactory),
            endpointManager, router, loggerFactory)
        {
        }

        public IEnumerable<ServiceEndpoint> GetOnlineEndpoints()
        {
            return Connections.Keys.Where(s => s.Online);
        }

        private static IServiceConnectionContainer CreateContainer(IServiceConnectionFactory serviceConnectionFactory, ServiceEndpoint endpoint, string hub, int count, IServiceEndpointManager endpointManager, IServerNameProvider nameProvider, ILoggerFactory loggerFactory)
        {
            var provider = endpointManager.GetEndpointProvider(endpoint);
            var connectionFactory = new ConnectionFactory(hub, provider, nameProvider, loggerFactory);
            if (endpoint.EndpointType == EndpointType.Primary)
            {
                return new StrongServiceConnectionContainer(serviceConnectionFactory, connectionFactory, count, endpoint);
            }
            else
            {
                return new WeakServiceConnectionContainer(serviceConnectionFactory, connectionFactory, count, endpoint);
            }
        }

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public Task ConnectionInitializedTask
        {
            get
            {
                if (_inner != null)
                {
                    return _inner.ConnectionInitializedTask;
                }

                return Task.WhenAll(from connection in Connections
                                    select connection.Value.ConnectionInitializedTask);
            }
        }

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

        public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            if (_inner != null)
            {
                return _inner.WriteAsync(partitionKey, serviceMessage);
            }

            // re-evaluate available endpoints as they might be offline, however it can not guarantee that all endpoints are online
            var routed = GetRoutedEndpoints(serviceMessage)?
                .Select(endpoint =>
                {
                    if (Connections.TryGetValue(endpoint, out var connection))
                    {
                        return connection.WriteAsync(partitionKey, serviceMessage);
                    }
                    Log.EndpointNotExists(_logger, endpoint.ToString());
                    return null;
                }).Where(s => s != null).ToArray();

            if (routed == null || routed.Length == 0)
            {
                Log.NoEndpointRouted(_logger, serviceMessage.GetType().Name);
                return Task.CompletedTask;
            }

            if (routed.Length == 1)
            {
                return routed[0];
            }

            return Task.WhenAll(routed);
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_inner != null)
            {
                return _inner.WriteAsync(serviceMessage);
            }

            var routed = GetRoutedEndpoints(serviceMessage)?
                .Select(endpoint =>
                {
                    if (Connections.TryGetValue(endpoint, out var connection))
                    {
                        return connection.WriteAsync(serviceMessage);
                    }
                    Log.EndpointNotExists(_logger, endpoint.ToString());
                    return null;
                }).Where(s => s != null).ToArray();
            if (routed == null || routed.Length == 0)
            {
                Log.NoEndpointRouted(_logger, serviceMessage.GetType().Name);
                return Task.CompletedTask;
            }

            if (routed.Length == 1)
            {
                return routed[0];
            }

            return Task.WhenAll(routed);
        }

        internal IEnumerable<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message)
        {
            var endpoints = _endpoints;
            switch (message)
            {
                case BroadcastDataMessage bdm:
                    return _router.GetEndpointsForBroadcast(endpoints);
                case GroupBroadcastDataMessage gbdm:
                    return _router.GetEndpointsForGroup(gbdm.GroupName, endpoints);
                case JoinGroupMessage jgm:
                    return _router.GetEndpointsForGroup(jgm.GroupName, endpoints);
                case LeaveGroupMessage lgm:
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

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _startingConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "StartingConnection"), "Staring connections for endpoint {endpoint}");

            private static readonly Action<ILogger, string, Exception> _stoppingConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "StoppingConnection"), "Stopping connections for endpoint {endpoint}");

            private static readonly Action<ILogger, string, Exception> _endpointNotExists =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "EndpointNotExists"), "Endpoint {endpoint} from the router does not exists");

            private static readonly Action<ILogger, string, Exception> _noEndpointRouted =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "NoEndpointRouted"), "The endpoint router returns no online endpoint for message {messageType} to route to. The message will not be sent.");

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

            public static void NoEndpointRouted(ILogger logger, string messageType)
            {
                _noEndpointRouted(logger, messageType, null);
            }
        }
    }
}
