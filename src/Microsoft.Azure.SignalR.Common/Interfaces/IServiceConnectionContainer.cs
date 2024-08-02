// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceConnectionContainer : IDisposable
    {
        ServiceConnectionStatus Status { get; }

        Task ConnectionInitializedTask { get; }

        string ServersTag { get; }

        bool HasClients { get; }

        Task StartAsync();

        Task StopAsync();

        Task OfflineAsync(GracefulShutdownMode mode);

        Task WriteAsync(ServiceMessage serviceMessage);

        Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default);

        Task StartGetServersPing();

        Task StopGetServersPing();
    }
}