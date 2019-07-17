// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientConnectionManager
    {
        void AddClientConnection(ServiceConnectionContext clientConnection);

        ServiceConnectionContext RemoveClientConnection(string connectionId);

        IReadOnlyDictionary<string, ServiceConnectionContext> ClientConnections { get; }
    }
}
