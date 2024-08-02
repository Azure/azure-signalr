// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestClientConnectionManager : IClientConnectionManager
{
    private readonly ConcurrentDictionary<string, IClientConnection> _dict = new();

    public IEnumerable<IClientConnection> ClientConnections => throw new System.NotImplementedException();

    public int Count => throw new System.NotImplementedException();

    public bool TryAddClientConnection(IClientConnection connection) => _dict.TryAdd(connection.ConnectionId, connection);

    public bool TryGetClientConnection(string connectionId, out IClientConnection connection) => _dict.TryGetValue(connectionId, out connection);

    public bool TryRemoveClientConnection(string connectionId, out IClientConnection connection) => _dict.TryRemove(connectionId, out connection);

    public Task WhenAllCompleted() => Task.CompletedTask;
}
