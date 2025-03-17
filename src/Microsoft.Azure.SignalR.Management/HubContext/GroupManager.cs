// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Manages groups in SignalR.
    /// </summary>
    public abstract class GroupManager : IGroupManager
    {
        /// <summary>
        /// Adds a connection to a group.
        /// </summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <param name="groupName">The group name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public abstract Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a connection from a group.
        /// </summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <param name="groupName">The group name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public abstract Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a connection from all groups.
        /// </summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public abstract Task RemoveFromAllGroupsAsync(string connectionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists connections in a group.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="top">The maximum number of connections to return.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An asynchronous enumerable of group members.</returns>
        internal virtual IAsyncEnumerable<GroupMember> ListConnectionsInGroup(string groupName, int? top = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
