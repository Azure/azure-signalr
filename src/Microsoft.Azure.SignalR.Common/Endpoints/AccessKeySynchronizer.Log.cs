// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal partial class AccessKeySynchronizer
    {
        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _failedAuthorize =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "FailedAuthorizeAccessKey"), "Failed in authorizing AccessKey for '{endpoint}', will retry in " + SignalR.AccessKeyForMicrosoftEntra.GetAccessKeyRetryIntervalInSec + " seconds");

            private static readonly Action<ILogger, string, Exception> _succeedAuthorize =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "SucceedAuthorizeAccessKey"), "Succeed in authorizing AccessKey for '{endpoint}'");

            public static void FailedToAuthorizeAccessKey(ILogger logger, string endpoint, Exception e)
            {
                _failedAuthorize(logger, endpoint, e);
            }

            public static void SucceedToAuthorizeAccessKey(ILogger logger, string endpoint)
            {
                _succeedAuthorize(logger, endpoint, null);
            }
        }
    }
}
