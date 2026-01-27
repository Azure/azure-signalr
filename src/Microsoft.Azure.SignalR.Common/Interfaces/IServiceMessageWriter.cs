// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Azure;

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal interface IServiceMessageWriter
{
    Task WriteAsync(ServiceMessage serviceMessage);

    Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Page<SignalRGroupMember>> ListConnectionsInGroupAsync(string groupName, int? top = null, int? maxPageSize = null, string? continuationToken = null, ulong? tracingId = null, CancellationToken token = default);
}
