// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A user group lifetime manager abstraction for adding and removing users from groups.
    /// </summary>
    public interface IUserGroupManager
    {
        /// <summary>
        /// Adds a user to the specified group.
        /// </summary>
        /// <param name="userId">The user ID to add to a group.</param>
        /// <param name="groupName">The group name.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is System.Threading.CancellationToken.None.</param>
        /// <returns>A System.Threading.Tasks.Task that represents the asynchronous add.</returns>
        Task AddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Removes a user from the specified group.
        /// </summary>
        /// <param name="userId">The user ID to remove from a group.</param>
        /// <param name="groupName">The group name.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is System.Threading.CancellationToken.None.</param>
        /// <returns>A System.Threading.Tasks.Task that represents the asynchronous remove.</returns>
        Task RemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);
    }
}