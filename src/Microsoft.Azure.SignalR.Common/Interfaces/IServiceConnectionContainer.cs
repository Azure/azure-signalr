// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceConnectionContainer
    {
        Task StartAsync();

        Task StopAsync();

        Task OfflineAsync();

        Task WriteAsync(ServiceMessage serviceMessage);

        Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default);

        ServiceConnectionStatus Status { get; }

        Task ConnectionInitializedTask { get; }

        /// <summary>
        /// Global connected Server Ids get from servers ping result from ASRS
        /// Invalid for MultiEndpointServiceConnectionContainer
        /// </summary>
        HashSet<string> GlobalServerIds { get; }

        /// <summary>
        /// Flag presents whether there's active clients from status ping result
        /// </summary>
        bool HasClients { get; }
    }
}