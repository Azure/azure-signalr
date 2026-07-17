// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
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

    Task<RefreshAuthResult> RefreshAuthAsync(string connectionToken, DateTimeOffset expireTime, IEnumerable<Claim>? claims, CancellationToken cancellationToken);

    Task<GetConnectionClaimsResult> GetConnectionClaimsAsync(string connectionToken, CancellationToken cancellationToken);

    AsyncPageable<SignalRGroupMember> ListConnectionsInGroup(string groupName, int? top = null, CancellationToken token = default);
}
