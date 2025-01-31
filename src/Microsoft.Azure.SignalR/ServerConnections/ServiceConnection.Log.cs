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
        private static readonly Action<ILogger, Exception> WaitingForTransportAction =
            LoggerMessage.Define(LogLevel.Debug, new EventId(2, "WaitingForTransport"), "Waiting for the transport layer to end.");

        private static readonly Action<ILogger, Exception> TransportCompleteAction =
            LoggerMessage.Define(LogLevel.Debug, new EventId(2, "TransportComplete"), "Transport completed.");

        private static readonly Action<ILogger, Exception> CloseTimedOutAction =
            LoggerMessage.Define(LogLevel.Debug, new EventId(3, "CloseTimedOut"), "Timed out waiting for close message sending to client, aborting the connection.");

        private static readonly Action<ILogger, Exception> WaitingForApplicationAction =
            LoggerMessage.Define(LogLevel.Debug, new EventId(4, "WaitingForApplication"), "Waiting for the application to end.");

        private static readonly Action<ILogger, Exception> ApplicationCompleteAction =
            LoggerMessage.Define(LogLevel.Debug, new EventId(4, "ApplicationComplete"), "Application task completes.");

        private static readonly Action<ILogger, Exception> FailedToCleanupConnectionsAction =
            LoggerMessage.Define(LogLevel.Error, new EventId(5, "FailedToCleanupConnection"), "Failed to clean up client connections.");

        private static readonly Action<ILogger, Exception> ApplicationTaskFailedAction =
            LoggerMessage.Define(LogLevel.Error, new EventId(8, "ApplicationTaskFailed"), "Application task failed.");

        private static readonly Action<ILogger, ulong?, string, Exception> ReceivedMessageForNonExistentConnectionAction =
            LoggerMessage.Define<ulong?, string>(LogLevel.Warning, new EventId(10, "ReceivedMessageForNonExistentConnection"), "Received message {tracingId} for connection {TransportConnectionId} which does not exist.");

        private static readonly Action<ILogger, string, Exception> ConnectedEndingAction =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(12, "ConnectedEnding"), "Connection {TransportConnectionId} ended.");

        private static readonly Action<ILogger, string, Exception> CloseConnectionAction =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(13, "CloseConnection"), "Sending close connection message to the service for {TransportConnectionId}.");

        private static readonly Action<ILogger, Exception> ApplicationTaskCancelledAction =
            LoggerMessage.Define(LogLevel.Error, new EventId(21, "ApplicationTaskCancelled"), "Cancelled running application code, probably caused by time out.");

        private static readonly Action<ILogger, string, Exception> ErrorSkippingHandshakeResponseAction =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(23, "ErrorSkippingHandshakeResponse"), "Error while skipping handshake response during migration, the connection will be dropped on the client-side. Error detail: {message}");

        private static readonly Action<ILogger, string, Exception> ProcessConnectionFailedAction =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(24, "ProcessConnectionFailed"), "Error processing the connection {TransportConnectionId}.");

        private static readonly Action<ILogger, int, string, Exception> ClosingClientConnectionsAction =
            LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(25, "ClosingClientConnections"), "Closing {ClientCount} client connection(s) for server connection {ServerConnectionId}.");

        public static void WaitingForTransport(ILogger logger)
        {
            WaitingForTransportAction(logger, null);
        }

        public static void TransportComplete(ILogger logger)
        {
            TransportCompleteAction(logger, null);
        }

        public static void CloseTimedOut(ILogger logger)
        {
            CloseTimedOutAction(logger, null);
        }

        public static void WaitingForApplication(ILogger logger)
        {
            WaitingForApplicationAction(logger, null);
        }

        public static void ApplicationComplete(ILogger logger)
        {
            ApplicationCompleteAction(logger, null);
        }

        public static void ClosingClientConnections(ILogger logger, int clientCount, string serverConnectionId)
        {
            ClosingClientConnectionsAction(logger, clientCount, serverConnectionId, null);
        }

        public static void FailedToCleanupConnections(ILogger logger, Exception exception)
        {
            FailedToCleanupConnectionsAction(logger, exception);
        }

        public static void ApplicationTaskFailed(ILogger logger, Exception exception)
        {
            ApplicationTaskFailedAction(logger, exception);
        }

        public static void ReceivedMessageForNonExistentConnection(ILogger logger, ConnectionDataMessage message)
        {
            ReceivedMessageForNonExistentConnectionAction(logger, message.TracingId, message.ConnectionId, null);
        }

        public static void ConnectedEnding(ILogger logger, string connectionId)
        {
            ConnectedEndingAction(logger, connectionId, null);
        }

        public static void CloseConnection(ILogger logger, string connectionId)
        {
            CloseConnectionAction(logger, connectionId, null);
        }

        public static void ApplicationTaskCancelled(ILogger logger)
        {
            ApplicationTaskCancelledAction(logger, null);
        }

        public static void ErrorSkippingHandshakeResponse(ILogger logger, Exception ex)
        {
            ErrorSkippingHandshakeResponseAction(logger, ex.Message, ex);
        }

        public static void ProcessConnectionFailed(ILogger logger, string connectionId, Exception exception)
        {
            ProcessConnectionFailedAction(logger, connectionId, exception);
        }
    }
}