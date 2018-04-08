// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    public class HubHostOptions
    {
        public static readonly int DefaultConnectionNumber = 5;
        private static readonly TimeSpan DefaultServerTimeout = TimeSpan.FromSeconds(30); // Server ping rate is 15 sec, this is 2 times that.

        public int ConnectionNumber { get; set; } = DefaultConnectionNumber;

        public TimeSpan ServerTimeout { get; set; } = DefaultServerTimeout;

        public Func<Task> OnConnected { get; set; } = null;

        public Func<Exception, Task> OnDisconnected { get; set; } = null;

        public bool AutoReconnect { get; set; } = true;
    }
}
