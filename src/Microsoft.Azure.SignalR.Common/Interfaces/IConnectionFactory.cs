// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR
{
    internal interface IConnectionFactory
    {
        Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken = default);

        // Current plan for IAsyncDisposable is that DisposeAsync will NOT take a CancellationToken
        // https://github.com/dotnet/csharplang/blob/195efa07806284d7b57550e7447dc8bd39c156bf/proposals/async-streams.md#iasyncdisposable
        Task DisposeAsync(ConnectionContext connection);
    }
}
