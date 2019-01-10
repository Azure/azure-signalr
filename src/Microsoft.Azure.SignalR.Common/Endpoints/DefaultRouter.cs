// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultRouter : IEndpointRouter
    {
        public ServiceEndpoint GetNegotiateEndpoint(IReadOnlyList<ServiceEndpoint> primaryEndpoints)
        {
            // get primary endpoints snapshot
            return primaryEndpoints[StaticRandom.Next(primaryEndpoints.Count)];
        }

        public IReadOnlyList<ServiceEndpoint> GetEndpointsForBroadcast(IReadOnlyList<ServiceEndpoint> availableEnpoints)
        {
            // broadcast to all the endpoints
            return availableEnpoints;
        }

        public IReadOnlyList<ServiceEndpoint> GetEndpointsForUser(string userId, IReadOnlyList<ServiceEndpoint> availableEnpoints)
        {
            return availableEnpoints;
        }

        public IReadOnlyList<ServiceEndpoint> GetEndpointsForUsers(IReadOnlyList<string> userList, IReadOnlyList<ServiceEndpoint> availableEnpoints)
        {
            return availableEnpoints;
        }

        public IReadOnlyList<ServiceEndpoint> GetEndpointsForGroup(string groupName, IReadOnlyList<ServiceEndpoint> availableEnpoints)
        {
            return availableEnpoints;
        }

        public IReadOnlyList<ServiceEndpoint> GetEndpointsForGroups(IReadOnlyList<string> groupList, IReadOnlyList<ServiceEndpoint> availableEnpoints)
        {
            return availableEnpoints;
        }

        public IReadOnlyList<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IReadOnlyList<ServiceEndpoint> availableEnpoints)
        {
            // broadcast to all the endpoints and service side to do the filter
            return availableEnpoints;
        }
    }
}
