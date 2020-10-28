// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// An interface version of <see cref="HubLifetimeManager{THub}"/> for multiple inheritance usage.
    /// </summary>
    internal interface IHubLifetimeManager
    {
        Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default);

        Task OnConnectedAsync(HubConnectionContext connection);

        Task OnDisconnectedAsync(HubConnectionContext connection);

        Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default);

        Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default);

        Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default);

        Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default);

        Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default);

        Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default);

        Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default);

        Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default);

        Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default);

        Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default);
    }
}
