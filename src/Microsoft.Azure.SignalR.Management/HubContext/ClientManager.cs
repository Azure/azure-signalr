// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A manager abstraction for managing the clients in a hub.
    /// </summary>
    public abstract class ClientManager
    {
        /// <summary>
        /// Close a connection asynchronously.
        /// </summary>
        /// <returns>The created <see cref="System.Threading.Tasks.Task{TResult}">Task</see> that represents the asynchronous operation.</returns>
        /// <remarks>To get the <paramref name="reason"/> from connection closed event, client should set <see cref="NegotiationOptions.EnableDetailedErrors"/> during negotiation.</remarks>
        public abstract Task CloseConnectionAsync(string connectionId, string reason = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a connection exists asynchronously.
        /// </summary>
        /// <returns>The created <see cref="System.Threading.Tasks.Task{TResult}">Task</see> that represents the asynchronous operation. True if the connection exists, otherwise false.</returns>
        public abstract Task<bool> ConnectionExistsAsync(string connectionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if any connections exists for a user asynchronously.
        /// </summary>
        /// <returns>The created <see cref="System.Threading.Tasks.Task{TResult}">Task</see> that represents the asynchronous operation. True if any connection exists, otherwise false.</returns>
        public abstract Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if any connections exists in a group asynchronously.
        /// </summary>
        /// <returns>The created <see cref="System.Threading.Tasks.Task{TResult}">Task</see> that represents the asynchronous operation. True if any connection exists, otherwise false.</returns>
        public abstract Task<bool> GroupExistsAsync(string groupName, CancellationToken cancellationToken = default);
    }
}