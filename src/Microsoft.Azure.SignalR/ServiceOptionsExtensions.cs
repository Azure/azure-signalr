// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR
{
    internal static class ServiceOptionsExtensions
    {
        public static void Validate(this ServiceOptions options)
        {
            if (options.MaxPollIntervalInSeconds.HasValue &&
                (options.MaxPollIntervalInSeconds < 1 || options.MaxPollIntervalInSeconds > 300))
            {
                throw new AzureSignalRInvalidServiceOptionsException("MaxPollIntervalInSeconds", "[1,300]");
            }
        }
    }
}
