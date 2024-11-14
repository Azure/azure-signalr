// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal interface IClientConnectionManager
{
    IEnumerable<IClientConnection> ClientConnections { get; }

    int Count { get; }

    bool TryAddClientConnection(IClientConnection connection);

    bool TryRemoveClientConnection(string connectionId, out IClientConnection? connection);

    bool TryGetClientConnection(string connectionId, out IClientConnection? connection);

    Task WhenAllCompleted();
}
