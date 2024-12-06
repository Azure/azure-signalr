// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR;

public interface IMessageRouter
{
    /// <summary>
    /// Get the service endpoints for broadcast message to send to
    /// </summary>
    /// <param name="endpoints">All the available endpoints</param>
    /// <returns></returns>
    IEnumerable<ServiceEndpoint> GetEndpointsForBroadcast(IEnumerable<ServiceEndpoint> endpoints);

    /// <summary>
    /// Get the service endpoints for the specified user to send to
    /// </summary>
    /// <param name="userId">The id of the user</param>
    /// <param name="endpoints">All the available endpoints</param>
    /// <returns></returns>
    IEnumerable<ServiceEndpoint> GetEndpointsForUser(string userId, IEnumerable<ServiceEndpoint> endpoints);

    /// <summary>
    /// Get the service endpoints for the specified group to send to
    /// </summary>
    /// <param name="groupName">The name of the group</param>
    /// <param name="endpoints">All the available endpoints</param>
    /// <returns></returns>
    IEnumerable<ServiceEndpoint> GetEndpointsForGroup(string groupName, IEnumerable<ServiceEndpoint> endpoints);

    /// <summary>
    /// Get the service endpoints for the specified connection to send to
    /// Note that this one is only called when the SDK is not able to identify where the connectionId is.
    /// When the outcoming connectionId happens to be also connected to this app server, SDK can directly send the messages back to that connectionId
    /// </summary>
    /// <param name="connectionId">The id of the connection</param>
    /// <param name="endpoints">All the available endpoints</param>
    /// <returns></returns>
    IEnumerable<ServiceEndpoint> GetEndpointsForConnection(string connectionId, IEnumerable<ServiceEndpoint> endpoints);
}
