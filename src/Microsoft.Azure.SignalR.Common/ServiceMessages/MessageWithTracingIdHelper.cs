// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal static class MessageWithTracingIdHelper
    {
        internal static ulong Prefix { get; set; } = (ulong)(Guid.NewGuid().GetHashCode() & 0x0FFF_FFFF) << 32;
        private static int _index = -1;

        // message tracing id is constructed in the format:
        // from most significant digit to least significant digit:
        // 8 hex digits: prefix of the server
        // 8 hex digits: message index
        // Usage:
        // - Messaging log category is enabled
        // - EnableMessageTracing is true for Management SDK
        public static ulong Generate()
        {
            return Prefix + (ulong)Interlocked.Increment(ref _index);
        }
    }
}