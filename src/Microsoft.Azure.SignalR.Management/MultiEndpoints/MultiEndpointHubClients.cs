// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiEndpointHubClients : IHubClients
    {
        private readonly IEndpointRouter _router;
        private readonly Dictionary<ServiceEndpoint, IHubClients> _hubClientsTable;
        private readonly IEnumerable<ServiceEndpoint> _endpoints;
        private readonly IEnumerable<IHubClients> _hubClients;

        internal MultiEndpointHubClients(IEndpointRouter router, Dictionary<ServiceEndpoint, IHubClients> hubClientsTable)
        {
            _router = router ?? throw new System.ArgumentNullException(nameof(router));
            _hubClientsTable = hubClientsTable ?? throw new System.ArgumentNullException(nameof(hubClientsTable));
            _endpoints = hubClientsTable.Keys;
            _hubClients = hubClientsTable.Values;
            All = new MultiEndpointClientProxy(_hubClients.Select(hubClient => hubClient.All));
        }

        public IClientProxy All { get; }

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)
        {
            return new MultiEndpointClientProxy(
_hubClients
.Select(hubClient => hubClient.AllExcept(excludedConnectionIds)));
        }

        public IClientProxy Client(string connectionId)
        {
            return new MultiEndpointClientProxy(
_router
.GetEndpointsForConnection(connectionId, _endpoints)
.Select(endpoint => _hubClientsTable[endpoint])
.Select(hubClient => hubClient.Client(connectionId)));
        }

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            return new MultiEndpointClientProxy(
connectionIds
.SelectMany(id => _router.GetEndpointsForConnection(id, _endpoints))
.Distinct()
.Select(endpoint => _hubClientsTable[endpoint])
.Select(hubClient => hubClient.Clients(connectionIds)));
        }

        public IClientProxy Group(string groupName)
        {
            return new MultiEndpointClientProxy(
_router
.GetEndpointsForGroup(groupName, _endpoints)
.Select(endpoint => _hubClientsTable[endpoint])
.Select(hubClient => hubClient.Group(groupName)));
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
        {
            return new MultiEndpointClientProxy(
_router
.GetEndpointsForGroup(groupName, _endpoints)
.Select(endpoint => _hubClientsTable[endpoint])
.Select(hubClient => hubClient.GroupExcept(groupName, excludedConnectionIds)));
        }

        public IClientProxy Groups(IReadOnlyList<string> groupNames)
        {
            return new MultiEndpointClientProxy(
groupNames
.SelectMany(groupName => _router.GetEndpointsForGroup(groupName, _endpoints))
.Distinct()
.Select(endpoint => _hubClientsTable[endpoint])
.Select(hubClient => hubClient.Groups(groupNames)));
        }

        public IClientProxy User(string userId)
        {
            return new MultiEndpointClientProxy(
_router
.GetEndpointsForUser(userId, _endpoints)
.Select(endpoint => _hubClientsTable[endpoint])
.Select(hubClient => hubClient.User(userId)));
        }

        public IClientProxy Users(IReadOnlyList<string> userIds)
        {
            return new MultiEndpointClientProxy(
userIds
.SelectMany(userId => _router.GetEndpointsForUser(userId, _endpoints))
.Distinct()
.Select(endpoint => _hubClientsTable[endpoint])
.Select(hubClient => hubClient.Users(userIds)));
        }
    }
}