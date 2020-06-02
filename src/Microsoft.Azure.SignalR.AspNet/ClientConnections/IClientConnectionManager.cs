// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal interface IClientConnectionManager : IClientConnectionLifetimeManager
    {
        Task<IServiceTransport> CreateConnection(OpenConnectionMessage message, IServiceConnection serviceConnection);

        bool TryAddClientConnection(ClientConnectionContext context);

        bool TryRemoveClientConnection(string connectionId, out ClientConnectionContext context);

        IReadOnlyDictionary<string, ClientConnectionContext> ClientConnections { get; }
    }
}
