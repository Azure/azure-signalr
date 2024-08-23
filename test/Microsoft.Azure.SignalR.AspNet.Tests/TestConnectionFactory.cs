// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal sealed class TestConnectionFactory : IConnectionFactory
{
    private TaskCompletionSource<TestConnectionContext> _waitForServerConnection = new TaskCompletionSource<TestConnectionContext>();

    public Task<ConnectionContext> ConnectAsync(HubServiceEndpoint endpoint, TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken = default, IDictionary<string, string> headers = null)
    {
        var connection = new TestConnectionContext();

        _waitForServerConnection.TrySetResult(connection);
        return Task.FromResult<ConnectionContext>(connection);
    }

    public Task DisposeAsync(ConnectionContext connection)
    {
        return Task.CompletedTask;
    }
}
