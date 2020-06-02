// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.Azure.SignalR
{
    internal static class MessageIdHelper
    {
        private static readonly int _prefix = Guid.NewGuid().GetHashCode();
        private static int _index = 0;

        // todo: returns ulong
        // message id is constructed in the format:
        // from most significant digit to least significant digit:
        // 1 hex digits: isFromTracingClient (unused bit are preserved),
        // 7 hex digits: prefix of the server
        // 8 hex digits: message index
        public static string Generate(bool isFromTracingClient)
        {
            ulong id = (((ulong)_prefix | (ulong)(isFromTracingClient ? 0x1000_0000 : 0)) << 32) + (ulong)_index;
            Interlocked.Increment(ref _index);
            return id.ToString();
        }
    }
}