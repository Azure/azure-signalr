// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal interface IServiceConnection
{
    string ConnectionId { get; }

    string ServerId { get; }

    ServiceConnectionStatus Status { get; }

    Task ConnectionInitializedTask { get; }

    Task ConnectionOfflineTask { get; }

    event Action<StatusChange> ConnectionStatusChanged;

    Task StartAsync(string? target = null);

    Task StopAsync();

    Task WriteAsync(ServiceMessage serviceMessage);

    Task<bool> SafeWriteAsync(ServiceMessage serviceMessage);
}
