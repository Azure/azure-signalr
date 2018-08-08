// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal class StaticRandom
    {
        private static readonly object RandomLock = new object();
        private static readonly Random RandomInterval = new Random((int)DateTime.UtcNow.Ticks);

        public static int Next(int maxValue)
        {
            lock (RandomLock)
            {
                return RandomInterval.Next(maxValue);
            }
        }
    }
}
