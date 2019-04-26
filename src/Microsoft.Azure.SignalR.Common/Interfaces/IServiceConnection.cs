// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceConnection
    {
        Task StartAsync(string target = null, string productInfo = null);

        Task WriteAsync(ServiceMessage serviceMessage);

        Task StopAsync();

        ServiceConnectionStatus Status { get; }

        Task ConnectionInitializedTask { get; }
    }
}
