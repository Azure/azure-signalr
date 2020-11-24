// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal static class HandshakeTimeoutHelper
    {
        public static int GetCustomHandshakeTimeout(TimeSpan? handshakeTimeout, ILogger logger)
        {
            if (!handshakeTimeout.HasValue)
            {
                Log.UseDefaultHandshakeTimeout(logger);
                return Constants.Periods.DefaultHandshakeTimeout;
            }

            var timeout = (int)handshakeTimeout.Value.TotalSeconds;

            // use default handshake timeout
            if (timeout == Constants.Periods.DefaultHandshakeTimeout)
            {
                Log.UseDefaultHandshakeTimeout(logger);
                return Constants.Periods.DefaultHandshakeTimeout;
            }

            // the custom handshake timeout is invalid, use default hanshake timeout instead
            if (timeout <= 0 || timeout > Constants.Periods.MaxCustomHandshakeTimeout)
            {
                Log.FailToSetCustomHandshakeTimeout(logger, new ArgumentOutOfRangeException(nameof(handshakeTimeout)));
                return Constants.Periods.DefaultHandshakeTimeout;
            }

            // the custom handshake timeout is valid
            Log.SucceedToSetCustomHandshakeTimeout(logger, timeout);
            return timeout;
        }

        // todo: move to a centralized log class
        private static class Log
        {
            private static readonly Action<ILogger, Exception> _useDefaultHandshakeTimeout =
                LoggerMessage.Define(LogLevel.Information, new EventId(0, "UseDefaultHandshakeTimeout"), "Use default handshake timeout.");

            private static readonly Action<ILogger, int, Exception> _succeedToSetCustomHandshakeTimeout =
                LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "SucceedToSetCustomHandshakeTimeout"), "Succeed to set custom handshake timeout: {timeout} seconds.");

            private static readonly Action<ILogger, Exception> _failToSetCustomHandshakeTimeout =
                LoggerMessage.Define(LogLevel.Warning, new EventId(2, "FailToSetCustomHandshakeTimeout"), $"Fail to set custom handshake timeout, use default handshake timeout {Constants.Periods.DefaultHandshakeTimeout} seconds instead. The range of custom handshake timeout should between 1 second to {Constants.Periods.MaxCustomHandshakeTimeout} seconds.");

            public static void UseDefaultHandshakeTimeout(ILogger logger)
            {
                _useDefaultHandshakeTimeout(logger, null);
            }

            public static void SucceedToSetCustomHandshakeTimeout(ILogger logger, int customHandshakeTimeout)
            {
                _succeedToSetCustomHandshakeTimeout(logger, customHandshakeTimeout, null);
            }

            public static void FailToSetCustomHandshakeTimeout(ILogger logger, Exception exception)
            {
                _failToSetCustomHandshakeTimeout(logger, exception);
            }
        }
    }
}
