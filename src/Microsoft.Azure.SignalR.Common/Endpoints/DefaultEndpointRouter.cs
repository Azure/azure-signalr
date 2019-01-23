// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.SignalR
{
    public class DefaultEndpointRouter : IEndpointRouter
    {
        /// <summary>
        /// Round robin from the available endpoints
        /// </summary>
        /// <param name="primaryEndpoints"></param>
        /// <returns></returns>
        public ServiceEndpoint GetNegotiateEndpoint(IEnumerable<ServiceEndpoint> primaryEndpoints)
        {
            // get primary endpoints snapshot
            var endpoints = primaryEndpoints.ToArray();
            return endpoints[StaticRandom.Next(endpoints.Length)];
        }

        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="availableEndpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> availableEndpoints)
        {
            // broadcast to all the endpoints
            return availableEndpoints;
        }

        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="availableEndpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> availableEndpoints)
        {
            return availableEndpoints;
        }

        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="userList"></param>
        /// <param name="availableEndpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForUsers(IReadOnlyList<string> userList, IEnumerable<ServiceEndpoint> availableEndpoints)
        {
            return availableEndpoints;
        }

        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="availableEndpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> availableEndpoints)
        {
            return availableEndpoints;
        }

        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="groupList"></param>
        /// <param name="availableEndpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForGroups(IReadOnlyList<string> groupList, IEnumerable<ServiceEndpoint> availableEndpoints)
        {
            return availableEndpoints;
        }

        /// <summary>
        /// Broadcast to all available endpoints
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="availableEndpoints"></param>
        /// <returns></returns>
        public IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> availableEndpoints)
        {
            // broadcast to all the endpoints and service side to do the filter
            return availableEndpoints;
        }
    }
}
