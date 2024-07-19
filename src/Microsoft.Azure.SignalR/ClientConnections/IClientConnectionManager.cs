// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientConnectionManager : IClientConnectionLifetimeManager
    {
        IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections { get; }

        bool TryAddClientConnection(ClientConnectionContext connection);

        bool TryRemoveClientConnection(string connectionId, out ClientConnectionContext connection);
    }
}
