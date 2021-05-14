// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal partial class ServiceConnection
    {
        private static class Log
        {
            // Category: ServiceConnection
            private static readonly Action<ILogger, Exception> _waitingForTransport =
                LoggerMessage.Define(LogLevel.Debug, new EventId(2, "WaitingForTransport"), "Waiting for the transport layer to end.");

            private static readonly Action<ILogger, Exception> _transportComplete =
                LoggerMessage.Define(LogLevel.Debug, new EventId(2, "TransportComplete"), "Transport completed.");

            private static readonly Action<ILogger, Exception> _closeTimedOut =
                LoggerMessage.Define(LogLevel.Debug, new EventId(3, "CloseTimedOut"), "Timed out waiting for close message sending to client, aborting the connection.");

            private static readonly Action<ILogger, Exception> _waitingForApplication =
                LoggerMessage.Define(LogLevel.Debug, new EventId(4, "WaitingForApplication"), "Waiting for the application to end.");

            private static readonly Action<ILogger, Exception> _applicationComplete =
                LoggerMessage.Define(LogLevel.Debug, new EventId(4, "ApplicationComplete"), "Application task completes.");

            private static readonly Action<ILogger, int, Exception> _startToCleanupClientConnections =
                LoggerMessage.Define<int>(LogLevel.Information, new EventId(5, "StartToCleanupClientConnections"), "Start to cleanup {clientCount} client connections, can be triggered by a service connection drop or app server shutdown.");

            private static readonly Action<ILogger, Exception> _failedToCleanupConnections =
                LoggerMessage.Define(LogLevel.Error, new EventId(5, "FailedToCleanupConnection"), "Failed to clean up client connections.");

            private static readonly Action<ILogger, string, Exception> _errorSendingMessage =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(6, "ErrorSendingMessage"), "Error while sending message to the service, the connection carrying the traffic is dropped. Error detail: {message}");

            private static readonly Action<ILogger, string, Exception> _sendLoopStopped =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(7, "SendLoopStopped"), "Error while processing messages from {TransportConnectionId}.");

            private static readonly Action<ILogger, Exception> _applicationTaskFailed =
                LoggerMessage.Define(LogLevel.Error, new EventId(8, "ApplicationTaskFailed"), "Application task failed.");

            private static readonly Action<ILogger, ulong?, string, Exception> _failToWriteMessageToApplication =
                LoggerMessage.Define<ulong?, string>(LogLevel.Error, new EventId(9, "FailToWriteMessageToApplication"), "Failed to write message {tracingId} to {TransportConnectionId}.");

            private static readonly Action<ILogger, ulong?, string, Exception> _receivedMessageForNonExistentConnection =
                LoggerMessage.Define<ulong?, string>(LogLevel.Warning, new EventId(10, "ReceivedMessageForNonExistentConnection"), "Received message {tracingId} for connection {TransportConnectionId} which does not exist.");

            private static readonly Action<ILogger, string, Exception> _connectedStarting =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(11, "ConnectedStarting"), "Connection {TransportConnectionId} started.");

            private static readonly Action<ILogger, string, Exception> _connectedEnding =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(12, "ConnectedEnding"), "Connection {TransportConnectionId} ended.");

            private static readonly Action<ILogger, string, Exception> _closeConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(13, "CloseConnection"), "Sending close connection message to the service for {TransportConnectionId}.");

            private static readonly Action<ILogger, long, string, Exception> _writeMessageToApplication =
                LoggerMessage.Define<long, string>(LogLevel.Trace, new EventId(19, "WriteMessageToApplication"), "Writing {ReceivedBytes} to connection {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _serviceConnectionConnected =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(20, "ServiceConnectionConnected"), "Service connection {ServiceConnectionId} connected.");

            private static readonly Action<ILogger, Exception> _applicationTaskCancelled =
                LoggerMessage.Define(LogLevel.Error, new EventId(21, "ApplicationTaskCancelled"), "Cancelled running application code, probably caused by time out.");

            private static readonly Action<ILogger, string, Exception> _migrationStarting =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(22, "MigrationStarting"), "Connection {TransportConnectionId} migrated from another server.");

            private static readonly Action<ILogger, string, Exception> _errorSkippingHandshakeResponse =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(23, "ErrorSkippingHandshakeResponse"), "Error while skipping handshake response during migration, the connection will be dropped on the client-side. Error detail: {message}");

            private static readonly Action<ILogger, string, Exception> _processConnectionFailed =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(24, "ProcessConnectionFailed"), "Error processing the connection {TransportConnectionId}.");

            public static void WaitingForTransport(ILogger logger)
            {
                _waitingForTransport(logger, null);
            }

            public static void TransportComplete(ILogger logger)
            {
                _transportComplete(logger, null);
            }

            public static void CloseTimedOut(ILogger logger)
            {
                _closeTimedOut(logger, null);
            }

            public static void WaitingForApplication(ILogger logger)
            {
                _waitingForApplication(logger, null);
            }

            public static void ApplicationComplete(ILogger logger)
            {
                _applicationComplete(logger, null);
            }

            public static void StartToCleanupClientConnection(ILogger logger, int clientCount)
            {
                _startToCleanupClientConnections(logger, clientCount, null);
            }

            public static void FailedToCleanupConnections(ILogger logger, Exception exception)
            {
                _failedToCleanupConnections(logger, exception);
            }

            public static void ErrorSendingMessage(ILogger logger, Exception exception)
            {
                _errorSendingMessage(logger, exception.Message, exception);
            }

            public static void SendLoopStopped(ILogger logger, string connectionId, Exception exception)
            {
                _sendLoopStopped(logger, connectionId, exception);
            }

            public static void ApplicationTaskFailed(ILogger logger, Exception exception)
            {
                _applicationTaskFailed(logger, exception);
            }

            public static void FailToWriteMessageToApplication(ILogger logger, ConnectionDataMessage message, Exception exception)
            {
                _failToWriteMessageToApplication(logger, message.TracingId, message.ConnectionId, exception);
            }

            public static void ReceivedMessageForNonExistentConnection(ILogger logger, ConnectionDataMessage message)
            {
                _receivedMessageForNonExistentConnection(logger, message.TracingId, message.ConnectionId, null);
            }

            public static void ConnectedStarting(ILogger logger, string connectionId)
            {
                _connectedStarting(logger, connectionId, null);
            }

            public static void MigrationStarting(ILogger logger, string connectionId)
            {
                _migrationStarting(logger, connectionId, null);
            }

            public static void ConnectedEnding(ILogger logger, string connectionId)
            {
                _connectedEnding(logger, connectionId, null);
            }

            public static void CloseConnection(ILogger logger, string connectionId)
            {
                _closeConnection(logger, connectionId, null);
            }

            public static void WriteMessageToApplication(ILogger logger, long count, string connectionId)
            {
                _writeMessageToApplication(logger, count, connectionId, null);
            }

            public static void ApplicationTaskCancelled(ILogger logger)
            {
                _applicationTaskCancelled(logger, null);
            }

            public static void ErrorSkippingHandshakeResponse(ILogger logger, Exception ex)
            {
                _errorSkippingHandshakeResponse(logger, ex.Message, ex);
            }

            public static void ProcessConnectionFailed(ILogger logger, string connectionId, Exception exception)
            {
                _processConnectionFailed(logger, connectionId, exception);
            }
        }
    }
}