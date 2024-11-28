// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.SignalR;

internal class DefaultMessageRouter : IMessageRouter
{
    /// <summary>
    /// Get all the online endpoints. When the endpoint is offline, there might be a need to notify user about it because there is potential message loss. Customer can choose to change the behavior by using a custom router.
    /// </summary>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> endpoints)
    {
        return endpoints.Where(s => s.Online);
    }

    /// <summary>
    ///  Get all the online endpoints. When the endpoint is offline, there might be a need to notify user about it because there is potential message loss. Customer can choose to change the behavior by using a custom router.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints)
    {
        return endpoints.Where(s => s.Online);
    }

    /// <summary>
    /// Get all the online endpoints. When the endpoint is offline, there might be a need to notify user about it because there is potential message loss. Customer can choose to change the behavior by using a custom router.
    /// </summary>
    /// <param name="groupName"></param>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints)
    {
        return endpoints.Where(s => s.Online);
    }

    /// <summary>
    /// Get all the online endpoints. When the endpoint is offline, there might be a need to notify user about it because there is potential message loss. Customer can choose to change the behavior by using a custom router.
    /// Note that this one is only called when the SDK is not able to identify where the connectionId is.
    /// When the outcoming connectionId happens to be also connected to this app server, SDK can directly send the messages back to that connectionId
    /// </summary>
    /// <param name="connectionId"></param>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints)
    {
        // broadcast to all the endpoints and service side to do the filter
        return endpoints.Where(s => s.Online);
    }
}
