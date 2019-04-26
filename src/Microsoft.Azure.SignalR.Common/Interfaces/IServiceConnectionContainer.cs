// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceConnectionContainer
    {
        Task StartAsync(string productInfo = null);

        Task StopAsync();

        Task WriteAsync(ServiceMessage serviceMessage);

        Task WriteAsync(string partitionKey, ServiceMessage serviceMessage);

        ServiceConnectionStatus Status { get; }

        Task ConnectionInitializedTask { get; }
    }
}