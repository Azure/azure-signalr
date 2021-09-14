﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceConnection
    {
        ServiceConnectionStatus Status { get; }

        Task ConnectionInitializedTask { get; }

        Task ConnectionOfflineTask { get; }

        event Action<StatusChange> ConnectionStatusChanged;

        Task StartAsync(string target = null);

        Task WriteAsync(ServiceMessage serviceMessage);

        Task StopAsync();
    }
}
