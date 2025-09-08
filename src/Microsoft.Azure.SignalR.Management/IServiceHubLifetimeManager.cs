// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using Azure;

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal interface IServiceHubLifetimeManager : IHubLifetimeManager, IUserGroupHubLifetimeManager, IStreamingHubLifetimeManager
{
    Task CloseConnectionAsync(string connectionId, string reason, CancellationToken cancellationToken);

    Task<bool> ConnectionExistsAsync(string connectionId, CancellationToken cancellationToken);

    Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken);

    Task<bool> GroupExistsAsync(string groupName, CancellationToken cancellationToken);

    AsyncPageable<SignalRGroupConnection> ListConnectionsInGroup(string groupName, int? top = null, CancellationToken token = default);
}
