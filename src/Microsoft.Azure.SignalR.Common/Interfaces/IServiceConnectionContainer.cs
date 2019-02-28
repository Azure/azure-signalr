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

        Task WriteAsync(ServiceMessage serviceMessage);

        Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default);

        ServiceConnectionStatus Status { get; }
    }
}