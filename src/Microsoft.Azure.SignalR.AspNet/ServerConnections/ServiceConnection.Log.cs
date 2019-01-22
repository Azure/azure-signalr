// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal partial class ServiceConnection
    {
        private static class Log
        {
            // Category: ServiceConnection
            private static readonly Action<ILogger, Exception> _failedToCleanupConnections =
                LoggerMessage.Define(LogLevel.Error, new EventId(5, "FailedToCleanupConnection"), "Failed to clean up client connections.");

            private static readonly Action<ILogger, string, Exception> _sendLoopStopped =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(7, "SendLoopStopped"), "Error while processing messages from {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _failToWriteMessageToApplication =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(9, "FailToWriteMessageToApplication"), "Failed to write message to {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _connectedStarting =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(11, "ConnectedStarting"), "Connection {TransportConnectionId} started.");

            private static readonly Action<ILogger, string, Exception> _connectedStartingFailed =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(11, "ConnectedStartingFailed"), "Connection {TransportConnectionId} failed to start.");

            private static readonly Action<ILogger, string, Exception> _duplicateConnectionId =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(11, "DuplicateConnectionId"), "Failed to create connection due to duplicate connection Id {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _connectedEnding =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(12, "ConnectedEnding"), "Connection {TransportConnectionId} ended.");

            private static readonly Action<ILogger, long, string, Exception> _writeMessageToApplication =
                LoggerMessage.Define<long, string>(LogLevel.Trace, new EventId(19, "WriteMessageToApplication"), "Writing {ReceivedBytes} to connection {TransportConnectionId}.");

            public static void FailedToCleanupConnections(ILogger logger, Exception exception)
            {
                _failedToCleanupConnections(logger, exception);
            }

            public static void SendLoopStopped(ILogger logger, string connectionId, Exception exception)
            {
                _sendLoopStopped(logger, connectionId, exception);
            }

            public static void FailToWriteMessageToApplication(ILogger logger, string connectionId, Exception exception)
            {
                _failToWriteMessageToApplication(logger, connectionId, exception);
            }

            public static void ConnectedStarting(ILogger logger, string connectionId)
            {
                _connectedStarting(logger, connectionId, null);
            }

            public static void ConnectedStartingFailed(ILogger logger, string connectionId, Exception e)
            {
                _connectedStartingFailed(logger, connectionId, e);
            }

            public static void DuplicateConnectionId(ILogger logger, string connectionId, Exception e)
            {
                _duplicateConnectionId(logger, connectionId, e);
            }

            public static void ConnectedEnding(ILogger logger, string connectionId)
            {
                _connectedEnding(logger, connectionId, null);
            }

            public static void WriteMessageToApplication(ILogger logger, long count, string connectionId)
            {
                _writeMessageToApplication(logger, count, connectionId, null);
            }
        }
    }
}
