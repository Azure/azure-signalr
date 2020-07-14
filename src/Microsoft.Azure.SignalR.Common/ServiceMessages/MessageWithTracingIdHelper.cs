// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal static class MessageWithTracingIdHelper
    {
        internal static ulong Prefix { get; set; } = (ulong)Guid.NewGuid().GetHashCode();
        private static long _index = 0;

        // message tracing id is constructed in the format:
        // from most significant digit to least significant digit:
        // 1 hex digits:
        //      1st lowest bit: isFromTracingClient
        //      2nd lowest bit: 
        //          direction:       
        //              0: server to client
        //              1: client to server
        // 7 hex digits: prefix of the server
        // 8 hex digits: message index
        public static ulong Generate(bool isFromTracingClient)
        {
            ulong prefixWithPreserve = Prefix & 0x0FFF_FFFF;
            ulong tracingClientMask = (ulong)(isFromTracingClient ? 0x1000_0000 : 0);
            var id = ((prefixWithPreserve | tracingClientMask) << 32) + (ulong)_index;
            Interlocked.Increment(ref _index);
            return id;
        }
    }
}