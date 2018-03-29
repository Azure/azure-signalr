// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR
{
    public class HubHostOptions
    {
        private static readonly int DefaultConnectionNumber = 5;
        private static readonly TransferFormat DefaultProtocolType = TransferFormat.Binary;
        // Server ping rate is 15 sec, this is 2 times that.
        private static readonly int DefaultServerTimeout = 30;

        public int ConnectionNumber { get; set; } = DefaultConnectionNumber;

        public TransferFormat ProtocolType { get; set; } = DefaultProtocolType;

        public int ServerTimeout { get; set; } = DefaultServerTimeout;

        public Func<Task> OnConnected { get; set; } = null;

        public Func<Exception, Task> OnDisconnected { get; set; } = null;

        public bool AutoReconnect { get; set; } = true;
    }
}
