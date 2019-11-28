// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientConnectionManager : IClientConnectionLifetimeManager
    {
        void AddClientConnection(ClientConnectionContext clientConnection);

        ClientConnectionContext RemoveClientConnection(string connectionId);

        IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections { get; }
    }
}
