// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal partial class ServiceConnection
    {
        private static class Log
        {
            // Category: ServiceConnection
            private static readonly Action<ILogger, Exception> _failedToWrite =
                LoggerMessage.Define(LogLevel.Error, new EventId(1, "FailedToWrite"), "Failed to send message to the service.");

            private static readonly Action<ILogger, Exception> _failedToConnect =
                LoggerMessage.Define(LogLevel.Error, new EventId(2, "FailedToConnect"), "Failed to connect to the service.");

            private static readonly Action<ILogger, Exception> _errorProcessingMessages =
                LoggerMessage.Define(LogLevel.Error, new EventId(3, "ErrorProcessingMessages"), "Error when processing messages.");

            private static readonly Action<ILogger, string, Exception> _connectionDropped =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "ConnectionDropped"), "Connection {ServiceConnectionId} to the service was dropped.");

            private static readonly Action<ILogger, Exception> _failToCleanupConnections =
                LoggerMessage.Define(LogLevel.Error, new EventId(5, "FailToCleanupConnection"), "Failed to clean up client connections..");

            private static readonly Action<ILogger, Exception> _errorSendingMessage =
                LoggerMessage.Define(LogLevel.Error, new EventId(6, "ErrorSendingMessage"), "Error while sending message to the service.");

            private static readonly Action<ILogger, string, Exception> _sendLoopStopped =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(7, "SendLoopStopped"), "Error while processing messages from {TransportConnectionId}.");

            private static readonly Action<ILogger, Exception> _applicationTaskFailed =
                LoggerMessage.Define(LogLevel.Error, new EventId(8, "ApplicationTaskFailed"), "Application task failed.");

            private static readonly Action<ILogger, string, Exception> _failToWriteMessageToApplication =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(9, "FailToWriteMessageToApplication"), "Failed to write message to {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _receivedMessageForNonExistentConnection =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(10, "ReceivedMessageForNonExistentConnection"), "Received message for connection {TransportConnectionId} which does not exist.");

            private static readonly Action<ILogger, string, Exception> _connectedStarting =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(11, "ConnectedStarting"), "Connection {TransportConnectionId} started.");

            private static readonly Action<ILogger, string, Exception> _connectedEnding =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(12, "ConnectedEnding"), "Connection {TransportConnectionId} ended.");

            private static readonly Action<ILogger, string, Exception> _closeConnection =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(13, "CloseConnection"), "Sending close connection message to the service for {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _serviceConnectionClosed =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(14, "serviceConnectionClose"), "Service connection {ServiceConnectionId} closed.");

            private static readonly Action<ILogger, string, Exception> _readingCanceled =
                LoggerMessage.Define<string>(LogLevel.Trace, new EventId(15, "ReadingCanceled"), "Reading from service connection {ServiceConnectionId} cancelled.");

            private static readonly Action<ILogger, long, string, Exception> _receivedMessage =
                LoggerMessage.Define<long, string>(LogLevel.Debug, new EventId(16, "ReceivedMessage"), "Received {ReceivedBytes} bytes from service {ServiceConnectionId}.");

            private static readonly Action<ILogger, double, Exception> _startingServerTimeoutTimer =
                LoggerMessage.Define<double>(LogLevel.Trace, new EventId(17, "StartingServerTimeoutTimer"), "Starting server timeout timer. Duration: {ServerTimeout:0.00}ms");

            private static readonly Action<ILogger, double, Exception> _serverTimeout =
                LoggerMessage.Define<double>(LogLevel.Error, new EventId(18, "ServerTimeout"), "Server timeout ({ServerTimeout:0.00}ms) elapsed without receiving a message from the server.");

            private static readonly Action<ILogger, Exception> _resettingKeepAliveTimer =
                LoggerMessage.Define(LogLevel.Trace, new EventId(19, "ResettingKeepAliveTimer"), "Resetting keep-alive timer, received a message from the server.");

            private static readonly Action<ILogger, int, string, Exception> _writeMessageToApplication =
                LoggerMessage.Define<int, string>(LogLevel.Trace, new EventId(20, "WriteMessageToApplication"), "Writing {ReceivedBytes} to connection {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _serviceConnectionConnected =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(21, "ServiceConnectionConnected"), "Service connection {ServiceConnectionId} connected.");

            public static void FailedToWrite(ILogger logger, Exception exception)
            {
                _failedToWrite(logger, exception);
            }

            public static void FailedToConnect(ILogger logger, Exception exception)
            {
                _failedToConnect(logger, exception);
            }

            public static void ErrorProcessingMessages(ILogger logger, Exception exception)
            {
                _errorProcessingMessages(logger, exception);
            }

            public static void ConnectionDropped(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _connectionDropped(logger, serviceConnectionId, exception);
            }

            public static void FailToCleanupConnections(ILogger logger, Exception exception)
            {
                _failToCleanupConnections(logger, exception);
            }

            public static void ErrorSendingMessage(ILogger logger, Exception exception)
            {
                _errorSendingMessage(logger, exception);
            }

            public static void SendLoopStopped(ILogger logger, string connectionId, Exception exception)
            {
                _sendLoopStopped(logger, connectionId, exception);
            }

            public static void ApplicaitonTaskFailed(ILogger logger, Exception exception)
            {
                _applicationTaskFailed(logger, exception);
            }

            public static void FailToWriteMessageToApplication(ILogger logger, string connectionId, Exception exception)
            {
                _failToWriteMessageToApplication(logger, connectionId, exception);
            }

            public static void ReceivedMessageForNonExistentConnection(ILogger logger, string connectionId)
            {
                _receivedMessageForNonExistentConnection(logger, connectionId, null);
            }

            public static void ConnectedStarting(ILogger logger, string connectionId)
            {
                _connectedStarting(logger, connectionId, null);
            }

            public static void ConnectedEnding(ILogger logger, string connectionId)
            {
                _connectedEnding(logger, connectionId, null);
            }

            public static void CloseConnection(ILogger logger, string connectionId)
            {
                _closeConnection(logger, connectionId, null);
            }

            public static void ServiceConnectionClosed(ILogger logger, string serviceConnectionId)
            {
                _serviceConnectionClosed(logger, serviceConnectionId, null);
            }

            public static void ServiceConnectionConnected(ILogger logger, string serviceConnectionId)
            {
                _serviceConnectionConnected(logger, serviceConnectionId, null);
            }

            public static void ReadingCanceled(ILogger logger, string serviceConnectionId)
            {
                _readingCanceled(logger, serviceConnectionId, null);
            }

            public static void ReceivedMessage(ILogger logger, long bytes, string serviceConnectionId)
            {
                _receivedMessage(logger, bytes, serviceConnectionId, null);
            }

            public static void StartingServerTimeoutTimer(ILogger logger, TimeSpan serverTimeout)
            {
                _startingServerTimeoutTimer(logger, serverTimeout.TotalMilliseconds, null);
            }

            public static void ServerTimeout(ILogger logger, TimeSpan serverTimeout)
            {
                _serverTimeout(logger, serverTimeout.TotalMilliseconds, null);
            }

            public static void ResettingKeepAliveTimer(ILogger logger)
            {
                _resettingKeepAliveTimer(logger, null);
            }

            public static void WriteMessageToApplication(ILogger logger, int count, string connectionId)
            {
                _writeMessageToApplication(logger, count, connectionId, null);
            }
        }
    }
}
