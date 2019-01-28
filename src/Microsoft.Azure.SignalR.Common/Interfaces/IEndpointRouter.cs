// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    public interface IEndpointRouter
    {
        /// <summary>
        /// Get the service endpoint for the client to connect to
        /// </summary>
        /// <param name="endpoints"></param>
        /// <returns></returns>
        ServiceEndpoint GetNegotiateEndpoint(IEnumerable<ServiceEndpoint> endpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> endpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints);

        IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints);
    }
}
