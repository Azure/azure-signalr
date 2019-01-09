// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        private readonly IServiceEndpointManager _endpointManager;
        private readonly IEndpointRouter _router;
        private readonly ILogger _logger;
        private readonly IServiceConnectionContainer _inner;

        public Dictionary<ServiceEndpoint, IServiceConnectionContainer> Connections { get; }

        public MultiEndpointServiceConnectionContainer(Func<ServiceEndpoint, IServiceConnectionContainer> generator, IServiceEndpointManager endpointManager, IEndpointRouter router, ILogger logger)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            _endpointManager = endpointManager ?? throw new ArgumentNullException(nameof(endpointManager));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var endpoints = endpointManager.GetAvailableEndpoints();
            if (endpoints.Count == 0)
            {
                throw new ArgumentException("No endpoint available.");
            }

            if (endpoints.Count == 1)
            {
                _inner = generator(endpoints[0]);
            }
            else
            {
                Connections = endpointManager.GetAvailableEndpoints().ToDictionary(s => s, s => generator(s));
            }
        }

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public Task StartAsync()
        {
            if (_inner != null)
            {
                return _inner.StartAsync();
            }

            return Task.WhenAll(Connections.Select(s =>
            {
                Log.StartingConnection(_logger, s.Key.Key);
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
                Log.StoppingConnection(_logger, s.Key.Key);
                return s.Value.StopAsync();
            }));
        }

        public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            if (_inner != null)
            {
                return _inner.WriteAsync(partitionKey, serviceMessage);
            }

            var routed = GetRoutedEndpoints(serviceMessage, _endpointManager.GetAvailableEndpoints());

            if (routed.Count == 0)
            {
                throw new AzureSignalRNotConnectedException();
            }

            return Task.WhenAll(routed.Select(s => Connections[s]).Select(s => s.WriteAsync(partitionKey, serviceMessage)));
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_inner != null)
            {
                return _inner.WriteAsync(serviceMessage);
            }

            var routed = GetRoutedEndpoints(serviceMessage, _endpointManager.GetAvailableEndpoints());

            if (routed.Count == 0)
            {
                throw new AzureSignalRNotConnectedException();
            }

            return Task.WhenAll(routed.Select(s => Connections[s]).Select(s => s.WriteAsync(serviceMessage)));
        }

        private IReadOnlyList<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message, IReadOnlyList<ServiceEndpoint> availableEndpoints)
        {
            switch (message)
            {
                case BroadcastDataMessage bdm:
                    return _router.GetEndpointsForBroadcast(availableEndpoints);
                case GroupBroadcastDataMessage gbdm:
                    return _router.GetEndpointsForGroup(gbdm.GroupName, availableEndpoints);
                case JoinGroupMessage jgm:
                    return _router.GetEndpointsForGroup(jgm.GroupName, availableEndpoints);
                case LeaveGroupMessage lgm:
                    return _router.GetEndpointsForGroup(lgm.GroupName, availableEndpoints);
                case MultiGroupBroadcastDataMessage mgbdm:
                    return _router.GetEndpointsForGroups(mgbdm.GroupList, availableEndpoints);
                case ConnectionDataMessage cdm:
                    return _router.GetEndpointsForConnection(cdm.ConnectionId, availableEndpoints);
                case UserDataMessage udm:
                    return _router.GetEndpointsForUser(udm.UserId, availableEndpoints);
                case MultiUserDataMessage mudm:
                    return _router.GetEndpointsForUsers(mudm.UserList, availableEndpoints);
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

            public static void StartingConnection(ILogger logger, string endpoint)
            {
                _startingConnection(logger, endpoint, null);
            }

            public static void StoppingConnection(ILogger logger, string endpoint)
            {
                _stoppingConnection(logger, endpoint, null);
            }
        }
    }
}
