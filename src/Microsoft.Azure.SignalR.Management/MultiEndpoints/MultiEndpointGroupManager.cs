// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiEndpointGroupManager : IGroupManager
    {
        private readonly IEndpointRouter _router;
        private readonly Dictionary<ServiceEndpoint, IGroupManager> _groupManagerTable;
        private readonly IEnumerable<ServiceEndpoint> _endpoints;

        internal MultiEndpointGroupManager(IEndpointRouter router, Dictionary<ServiceEndpoint, IGroupManager> groupManagerTable)
        {
            _router = router ?? throw new System.ArgumentNullException(nameof(router));
            _groupManagerTable = groupManagerTable ?? throw new System.ArgumentNullException(nameof(groupManagerTable));
            _endpoints = groupManagerTable.Keys;
        }

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.WhenAll(
                GetEndpointsForConnectionAndGroup(connectionId, groupName)
                    .Select(endpoint => _groupManagerTable[endpoint])
                    .Select(groupManager => groupManager.AddToGroupAsync(connectionId, groupName, cancellationToken)));

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) =>
            Task.WhenAll(
                GetEndpointsForConnectionAndGroup(connectionId, groupName)
                    .Select(endpoint => _groupManagerTable[endpoint])
                    .Select(groupManager => groupManager.RemoveFromGroupAsync(connectionId, groupName, cancellationToken)));

        private IEnumerable<ServiceEndpoint> GetEndpointsForConnectionAndGroup(string connectionId, string groupName) => _router
                    .GetEndpointsForConnection(connectionId, _endpoints)
                    .Intersect(_router.GetEndpointsForGroup(groupName, _endpoints));
    }
}