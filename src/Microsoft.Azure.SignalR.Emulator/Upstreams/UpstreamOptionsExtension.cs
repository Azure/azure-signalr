// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Emulator
{
    internal static class UpstreamOptionsExtension
    {
        public static void Print(this UpstreamOptions options)
        {
            Console.WriteLine($"Current Upstream Settings:\n{options}");
        }
    }
}
