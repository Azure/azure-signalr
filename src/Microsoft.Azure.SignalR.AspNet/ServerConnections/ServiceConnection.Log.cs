// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet;

internal partial class ServiceConnection
{
    private static class Log
    {
        // Category: ServiceConnection
        private static readonly Action<ILogger, Exception> _failedToCleanupConnections =
            LoggerMessage.Define(LogLevel.Error, new EventId(1, "FailedToCleanupConnection"), "Failed to clean up client connections.");

        private static readonly Action<ILogger, string, Exception> _sendLoopStopped =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, "SendLoopStopped"), "Error while processing messages from {TransportConnectionId}.");

        private static readonly Action<ILogger, string, string, ulong?, Exception> _failToWriteMessageToApplication =
            LoggerMessage.Define<string, string, ulong?>(LogLevel.Error, new EventId(3, "FailToWriteMessageToApplication"), "Failed to write {messageType} message {tracingId} to {TransportConnectionId}.");

        private static readonly Action<ILogger, string, Exception> _connectedStarting =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(4, "ConnectedStarting"), "Connection {TransportConnectionId} started.");

        private static readonly Action<ILogger, string, Exception> _connectedStartingFailed =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(5, "ConnectedStartingFailed"), "Connection {TransportConnectionId} failed to start.");

        private static readonly Action<ILogger, string, Exception> _duplicateConnectionId =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(6, "DuplicateConnectionId"), "Duplicate OpenConnectionMessage for connection Id {TransportConnectionId}, ignored.");

        private static readonly Action<ILogger, string, Exception> _connectedEnding =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(7, "ConnectedEnding"), "Connection {TransportConnectionId} ended.");

        private static readonly Action<ILogger, long, string, Exception> _writeMessageToApplication =
            LoggerMessage.Define<long, string>(LogLevel.Trace, new EventId(8, "WriteMessageToApplication"), "Writing {ReceivedBytes} to connection {TransportConnectionId}.");

        private static readonly Action<ILogger, Exception> _applicationTaskFailed =
            LoggerMessage.Define(LogLevel.Error, new EventId(9, "ApplicationTaskFailed"), "Application task failed.");

        private static readonly Action<ILogger, Exception> _applicationTaskTimedOut =
            LoggerMessage.Define(LogLevel.Error, new EventId(10, "ApplicationTaskTimedOut"), "Timed out waiting for the application task to complete.");

        private static readonly Action<ILogger, int, string, Exception> _closingClientConnections =
            LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(11, "ClosingClientConnections"), "Closing {ClientCount} client connection(s) for server connection {ServerConnectionId}.");

        public static void ClosingClientConnections(ILogger logger, int clientCount, string serverConnectionId)
        {
            _closingClientConnections(logger, clientCount, serverConnectionId, null);
        }

        public static void FailedToCleanupConnections(ILogger logger, Exception exception)
        {
            _failedToCleanupConnections(logger, exception);
        }

        public static void SendLoopStopped(ILogger logger, string connectionId, Exception exception)
        {
            _sendLoopStopped(logger, connectionId, exception);
        }

        public static void FailToWriteMessageToApplication(ILogger logger, string messageType, string connectionId, ulong? tracingId, Exception exception)
        {
            _failToWriteMessageToApplication(logger, messageType, connectionId, tracingId, exception);
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

        public static void ApplicationTaskFailed(ILogger logger, Exception exception)
        {
            _applicationTaskFailed(logger, exception);
        }

        public static void ApplicationTaskTimedOut(ILogger logger)
        {
            _applicationTaskTimedOut(logger, null);
        }
    }
}