// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal interface IServiceConnectionContainer : IServiceConnectionManager,  IDisposable
{
    ServiceConnectionStatus Status { get; }

    Task ConnectionInitializedTask { get; }

    string ServersTag { get; }

    bool HasClients { get; }

    Task StartGetServersPing();

    Task StopGetServersPing();

    Task<RefreshAuthResult> RefreshAuthAsync(RefreshAuthMessage message, HubServiceEndpoint? preferredEndpoint = null, CancellationToken cancellationToken = default);

    Task<GetConnectionClaimsResult> GetConnectionClaimsAsync(GetConnectionClaimsMessage message, CancellationToken cancellationToken = default);
}

