// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

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

        private static readonly Action<ILogger, Exception> _failedToCleanupConnections =
            LoggerMessage.Define(LogLevel.Error, new EventId(5, "FailedToCleanupConnection"), "Failed to clean up client connections.");

        private static readonly Action<ILogger, Exception> _applicationTaskFailed =
            LoggerMessage.Define(LogLevel.Error, new EventId(8, "ApplicationTaskFailed"), "Application task failed.");

        private static readonly Action<ILogger, ulong?, string, Exception> _failToWriteMessageToApplication =
            LoggerMessage.Define<ulong?, string>(LogLevel.Error, new EventId(9, "FailToWriteMessageToApplication"), "Failed to write message {tracingId} to {TransportConnectionId}.");

        private static readonly Action<ILogger, ulong?, string, Exception> _receivedMessageForNonExistentConnection =
            LoggerMessage.Define<ulong?, string>(LogLevel.Warning, new EventId(10, "ReceivedMessageForNonExistentConnection"), "Received message {tracingId} for connection {TransportConnectionId} which does not exist.");

        private static readonly Action<ILogger, string, Exception> _connectedEnding =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(12, "ConnectedEnding"), "Connection {TransportConnectionId} ended.");

        private static readonly Action<ILogger, string, Exception> _closeConnection =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(13, "CloseConnection"), "Sending close connection message to the service for {TransportConnectionId}.");

        private static readonly Action<ILogger, long, string, Exception> _writeMessageToApplication =
            LoggerMessage.Define<long, string>(LogLevel.Trace, new EventId(19, "WriteMessageToApplication"), "Writing {ReceivedBytes} to connection {TransportConnectionId}.");

        private static readonly Action<ILogger, string, Exception> _serviceConnectionConnected =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(20, "ServiceConnectionConnected"), "Service connection {ServiceConnectionId} connected.");

        private static readonly Action<ILogger, Exception> _applicationTaskCancelled =
            LoggerMessage.Define(LogLevel.Error, new EventId(21, "ApplicationTaskCancelled"), "Cancelled running application code, probably caused by time out.");

        private static readonly Action<ILogger, string, Exception> _errorSkippingHandshakeResponse =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(23, "ErrorSkippingHandshakeResponse"), "Error while skipping handshake response during migration, the connection will be dropped on the client-side. Error detail: {message}");

        private static readonly Action<ILogger, string, Exception> _processConnectionFailed =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(24, "ProcessConnectionFailed"), "Error processing the connection {TransportConnectionId}.");

        private static readonly Action<ILogger, int, string, Exception> _closingClientConnections =
            LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(25, "ClosingClientConnections"), "Closing {ClientCount} client connection(s) for server connection {ServerConnectionId}.");

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

        public static void ClosingClientConnections(ILogger logger, int clientCount, string serverConnectionId)
        {
            _closingClientConnections(logger, clientCount, serverConnectionId, null);
        }

        public static void FailedToCleanupConnections(ILogger logger, Exception exception)
        {
            _failedToCleanupConnections(logger, exception);
        }

        public static void ApplicationTaskFailed(ILogger logger, Exception exception)
        {
            _applicationTaskFailed(logger, exception);
        }

        public static void ReceivedMessageForNonExistentConnection(ILogger logger, ConnectionDataMessage message)
        {
            _receivedMessageForNonExistentConnection(logger, message.TracingId, message.ConnectionId, null);
        }

        public static void ConnectedEnding(ILogger logger, string connectionId)
        {
            _connectedEnding(logger, connectionId, null);
        }

        public static void CloseConnection(ILogger logger, string connectionId)
        {
            _closeConnection(logger, connectionId, null);
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