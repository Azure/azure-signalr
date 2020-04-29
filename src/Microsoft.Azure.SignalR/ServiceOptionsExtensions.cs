// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    internal static class ServiceOptionsExtensions
    {
        public static void Validate(this ServiceOptions options)
        {
            if (options.DisconnectTimeoutInSeconds.HasValue &&
                (options.DisconnectTimeoutInSeconds < 1 || options.DisconnectTimeoutInSeconds > 300))
            {
                throw new ArgumentOutOfRangeException("DisconnectTimeoutInSeconds", "Value should be [1,300].");
            }
        }
    }
}
