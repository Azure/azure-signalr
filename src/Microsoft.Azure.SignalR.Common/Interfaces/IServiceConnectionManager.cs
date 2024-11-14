// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal interface IServiceConnectionManager : IServiceMessageWriter
{
    Task StartAsync();

    Task StopAsync();

    Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token);

    Task CloseClientConnections(CancellationToken token);
}