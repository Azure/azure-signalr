// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal interface IServiceConnectionContainer : IServiceConnectionManager, IDisposable
{
    ServiceConnectionStatus Status { get; }

    Task ConnectionInitializedTask { get; }

    string ServersTag { get; }

    bool HasClients { get; }

    Task StartGetServersPing();

    Task StopGetServersPing();
}