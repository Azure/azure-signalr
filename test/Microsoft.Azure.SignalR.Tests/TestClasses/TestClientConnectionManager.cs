// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Tests;

internal sealed class TestClientConnectionManager : IClientConnectionManager
{
    public int CompleteIndex = -1;

    private readonly ClientConnectionManager _ccm = new ClientConnectionManager();

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ClientConnectionContext>> _tcs =
        new ConcurrentDictionary<string, TaskCompletionSource<ClientConnectionContext>>();

    private readonly ConcurrentDictionary<string, TaskCompletionSource<ClientConnectionContext>> _tcsForRemoval
        = new ConcurrentDictionary<string, TaskCompletionSource<ClientConnectionContext>>();

    private readonly StrongBox<int> _index;

    public IEnumerable<IClientConnection> ClientConnections => _ccm.ClientConnections;

    public int Count => _ccm.Count;

    public TestClientConnectionManager() : this(new StrongBox<int>())
    {
    }

    public TestClientConnectionManager(StrongBox<int> index)
    {
        _index = index;
    }

    public Task<ClientConnectionContext> WaitForClientConnectionRemovalAsync(string id)
    {
        var tcs = _tcsForRemoval.GetOrAdd(id,
            s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                .RunContinuationsAsynchronously));
        return tcs.Task;
    }

    public Task<ClientConnectionContext> WaitForClientConnectionAsync(string id)
    {
        var tcs = _tcs.GetOrAdd(id,
            s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                .RunContinuationsAsynchronously));
        return tcs.Task;
    }

    public bool TryAddClientConnection(IClientConnection connection) => TryAddClientConnection(connection as ClientConnectionContext);

    public bool TryRemoveClientConnection(string connectionId, out IClientConnection connection)
    {
        var tcs = _tcsForRemoval.GetOrAdd(connectionId,
            s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                .RunContinuationsAsynchronously));
        _tcs.TryRemove(connectionId, out _);
        var r = _ccm.TryRemoveClientConnection(connectionId, out connection);
        tcs.TrySetResult(connection as ClientConnectionContext);
        return r;
    }

    public bool TryGetClientConnection(string connectionId, out IClientConnection connection)
    {
        return _ccm.TryGetClientConnection(connectionId, out connection);
    }

    public async Task WhenAllCompleted()
    {
        await Task.Yield();
        CompleteIndex = Interlocked.Increment(ref _index.Value);
    }

    private bool TryAddClientConnection(ClientConnectionContext connection)
    {
        var tcs = _tcs.GetOrAdd(connection.ConnectionId,
            s => new TaskCompletionSource<ClientConnectionContext>(TaskCreationOptions
                .RunContinuationsAsynchronously));
        var r = _ccm.TryAddClientConnection(connection);
        tcs.SetResult(connection);
        return r;
    }
}
