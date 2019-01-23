// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultRouter : IEndpointRouter
    {
        public ServiceEndpoint GetNegotiateEndpoint(IEnumerable<ServiceEndpoint> primaryEndpoints)
        {
            // get primary endpoints snapshot
            var endpoints = primaryEndpoints.ToArray();
            return endpoints[StaticRandom.Next(endpoints.Length)];
        }

        public IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> availableEnpoints)
        {
            // broadcast to all the endpoints
            return availableEnpoints;
        }

        public IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> availableEnpoints)
        {
            return availableEnpoints;
        }

        public IEnumerable<ServiceEndpoint> GetEndpointsForUsers(IReadOnlyList<string> userList, IEnumerable<ServiceEndpoint> availableEnpoints)
        {
            return availableEnpoints;
        }

        public IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> availableEnpoints)
        {
            return availableEnpoints;
        }

        public IEnumerable<ServiceEndpoint> GetEndpointsForGroups(IReadOnlyList<string> groupList, IEnumerable<ServiceEndpoint> availableEnpoints)
        {
            return availableEnpoints;
        }

        public IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> availableEnpoints)
        {
            // broadcast to all the endpoints and service side to do the filter
            return availableEnpoints;
        }
    }
}
