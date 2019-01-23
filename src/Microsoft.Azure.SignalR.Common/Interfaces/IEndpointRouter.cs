// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    public interface IEndpointRouter
    {
        /// <summary>
        /// TODO: add HttpContext for Core and HostContext for AspNet one?
        /// </summary>
        /// <param name="primaryEndpoints"></param>
        /// <returns></returns>
        ServiceEndpoint GetNegotiateEndpoint(IEnumerable<ServiceEndpoint> primaryEndpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> availableEndpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> availableEndpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForUsers(IReadOnlyList<string> userList, IEnumerable<ServiceEndpoint> availableEndpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> availableEndpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForGroups(IReadOnlyList<string> groupList, IEnumerable<ServiceEndpoint> availableEndpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> availableEndpoints);
    }
}
