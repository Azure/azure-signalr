// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR
{
    internal static class ServiceOptionsExtensions
    {
        private static readonly Regex AppNameRegex = new Regex("^[a-zA-Z][a-zA-Z0-9_]*$", RegexOptions.Compiled);
        public static void Validate(this ServiceOptions options)
        {
            if (options.MaxPollIntervalInSeconds.HasValue &&
                (options.MaxPollIntervalInSeconds < 1 || options.MaxPollIntervalInSeconds > 300))
            {
                throw new AzureSignalRInvalidServiceOptionsException(nameof(options.MaxPollIntervalInSeconds), "[1,300]");
            }

            // == 0 can be valid for serverless workaround
            if (options.InitialHubServerConnectionCount < 0)
            {
                throw new AzureSignalRInvalidServiceOptionsException(nameof(options.InitialHubServerConnectionCount), "> 0");
            }

            if (options.MaxHubServerConnectionCount.HasValue && options.MaxHubServerConnectionCount < options.InitialHubServerConnectionCount)
            {
                throw new AzureSignalRInvalidServiceOptionsException(nameof(options.MaxHubServerConnectionCount), $">= {options.InitialHubServerConnectionCount}");
            }

            if (!string.IsNullOrEmpty(options.ApplicationName) && !AppNameRegex.IsMatch(options.ApplicationName))
            {
                throw new AzureSignalRInvalidServiceOptionsException(nameof(options.ApplicationName), "prefixed with alphabetic characters and only contain alpha-numeric characters or underscore");
            }
        }
    }
}
