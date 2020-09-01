// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Serverless.Common;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal interface IUpstreamMessageHandler
    {
        Task AddClientConnectionAsync(HubConnectionContext connection, CancellationToken token = default);

        Task RemoveClientConnectionAsync(HubConnectionContext connection, string error, CancellationToken token = default);
        
        Task<ReadOnlySequence<byte>> WriteMessageAsync(HubConnectionContext connection, ServerlessProtocol.InvocationMessage message, CancellationToken token = default);
    }
}
