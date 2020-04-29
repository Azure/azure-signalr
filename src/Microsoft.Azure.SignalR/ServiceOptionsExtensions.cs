// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR
{
    internal static class ServiceOptionsExtensions
    {
        public static void Validate(this ServiceOptions options)
        {
            if (options.DisconnectTimeoutInSeconds.HasValue &&
                (options.DisconnectTimeoutInSeconds < 1 || options.DisconnectTimeoutInSeconds > 300))
            {
                throw new AzureSignalRInvalidServiceOptionsException("DisconnectTimeoutInSeconds", "[1,300]");
            }
        }
    }
}
