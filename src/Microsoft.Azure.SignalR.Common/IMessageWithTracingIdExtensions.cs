// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class IMessageWithTracingIdExtensions
    {
        private static int _prefix = Guid.NewGuid().GetHashCode();
        private static int counter = 0;

        public static void UpdateTracingId(this IMessageWithTracingId msg)
        {
            ulong id = (ulong)_prefix / 0x10 * 0x100000000 + (ulong)counter;
            msg.TracingId = $"{id:X}"; // todo: change TracingId to ulong
            Interlocked.Increment(ref counter);
        }
    }
}
