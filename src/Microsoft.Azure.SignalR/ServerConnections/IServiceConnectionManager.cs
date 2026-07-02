// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal interface IServiceConnectionManager<THub> : IServiceConnectionManager where THub : Hub
{
    void SetServiceConnection(IServiceConnectionContainer serviceConnection);

    Task<RefreshConnectionAuthResult> RefreshConnectionAuthAsync(RefreshAuthMessage message, CancellationToken cancellationToken = default);

    Task<(AckStatus Status, IReadOnlyList<Claim>? Claims)> GetConnectionClaimsAsync(GetConnectionClaimsMessage message, CancellationToken cancellationToken = default);
}

