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
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        private readonly IMessageRouter _router;
        private readonly ILogger _logger;
        private readonly IServiceConnectionContainer _inner;

        private IReadOnlyList<HubServiceEndpoint> _endpoints;

        public Dictionary<ServiceEndpoint, IServiceConnectionContainer> Connections { get; }

        public MultiEndpointServiceConnectionContainer(string hub, Func<HubServiceEndpoint, IServiceConnectionContainer> generator, IServiceEndpointManager endpointManager, IMessageRouter router, ILoggerFactory loggerFactory)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            _logger = loggerFactory?.CreateLogger<MultiEndpointServiceConnectionContainer>() ?? NullLogger<MultiEndpointServiceConnectionContainer>.Instance;

            // provides a copy to the endpoint per container
            _endpoints = endpointManager.GetEndpoints(hub);

            if (_endpoints.Count == 1)
            {
                _inner = generator(_endpoints[0]);
            }
            else
            {
                // router is required when endpoints > 1
                _router = router ?? throw new ArgumentNullException(nameof(router));
                Connections = _endpoints.ToDictionary(s => (ServiceEndpoint)s, s => generator(s));
            }
        }

        public MultiEndpointServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory, string hub,
            int count, IServiceEndpointManager endpointManager, IMessageRouter router, IServerNameProvider nameProvider, ILoggerFactory loggerFactory)
        :this(hub, endpoint => CreateContainer(serviceConnectionFactory, endpoint, count, loggerFactory),
            endpointManager, router, loggerFactory)
        {
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

            var writeMessageTask = WriteMultiEndpointMessageAsync(serviceMessage, async connection => {
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
            var endpoints = _endpoints;
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

            public static void FailedWritingMessageToEndpoint(ILogger logger, string messageType, string endpoint)
            {
                _failedWritingMessageToEndpoint(logger, messageType, endpoint, null);
            }
        }
    }
}
