// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

internal abstract partial class ServiceConnectionBase
{
    private static class Log
    {
        // Category: ServiceConnection
        // Debug: message related info
        // Information: connection related info
        // Warning: customer to aware
        // Error: application error
        private static readonly Action<ILogger, ulong?, string, string, Exception> _failedToWrite =
            LoggerMessage.Define<ulong?, string, string>(LogLevel.Error, new EventId(1, "FailedToWrite"), "Failed to send message {TracingId} to the service. Error detail: {Error}. Id: {ServiceConnectionId}");

        private static readonly Action<ILogger, string, string, string, Exception> _failedToConnect =
            LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(2, "FailedToConnect"), "Failed to connect to '{Endpoint}', will retry after the back off period. Error detail: {Error}. Id: {ServiceConnectionId}");

        private static readonly Action<ILogger, string, string, Exception> _errorProcessingMessages =
            LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(3, "ErrorProcessingMessages"), "Error when processing messages from service '{Endpoint}'. Id: {ServiceConnectionId}");

        private static readonly Action<ILogger, string, string, string, Exception> _connectionDropped =
            LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(4, "ConnectionDropped"), "Connection to '{Endpoint}' was dropped, probably caused by network instability or service restart. Will reconnect after the back off period. Error detail: {Error}. Id: {ServiceConnectionId}.");

        private static readonly Action<ILogger, string, Exception> _serviceConnectionClosed =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(14, "ServiceConnectionClose"), "Service connection {ServiceConnectionId} closed.");

        private static readonly Action<ILogger, string, Exception> _readingCancelled =
            LoggerMessage.Define<string>(LogLevel.Trace, new EventId(15, "ReadingCancelled"), "Reading from service connection {ServiceConnectionId} cancelled.");

        private static readonly Action<ILogger, long, string, Exception> _receivedMessage =
            LoggerMessage.Define<long, string>(LogLevel.Debug, new EventId(16, "ReceivedMessage"), "Received {ReceivedBytes} bytes from service {ServiceConnectionId}.");

        private static readonly Action<ILogger, double, Exception> _startingKeepAliveTimer =
            LoggerMessage.Define<double>(LogLevel.Trace, new EventId(17, "StartingKeepAliveTimer"), "Starting keep-alive timer. Duration: {KeepAliveInterval:0.00}ms");

        private static readonly Action<ILogger, string, double, string, Exception> _serviceTimeout =
            LoggerMessage.Define<string, double, string>(LogLevel.Information, new EventId(18, "ServiceTimeout"), "Connection to service '{Endpoint}' timeout. {ServiceTimeout:0.00}ms elapsed without receiving a message from service. Id: {ServiceConnectionId}");

        private static readonly Action<ILogger, string, Exception> _serviceConnectionConnected =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(20, "ServiceConnectionConnected"), "Service connection {ServiceConnectionId} connected.");

        private static readonly Action<ILogger, Exception> _sendingHandshakeRequest =
            LoggerMessage.Define(LogLevel.Debug, new EventId(21, "SendingHandshakeRequest"), "Sending Handshake request to service.");

        private static readonly Action<ILogger, Exception> _handshakeComplete =
            LoggerMessage.Define(LogLevel.Debug, new EventId(22, "HandshakeComplete"), "Handshake with service completes.");

        private static readonly Action<ILogger, string, string, string, Exception> _handshakeError =
            LoggerMessage.Define<string, string, string>(LogLevel.Information, new EventId(24, "HandshakeError"), "Connection to service '{Endpoint}' handshake failed, probably caused by network instability or service restart. Will retry after the back off period. Error detail: {Error}. Id: {ServiceConnectionId}");

        private static readonly Action<ILogger, Exception> _sentPing =
            LoggerMessage.Define(LogLevel.Debug, new EventId(25, "SentPing"), "Sent a ping message to service.");

        private static readonly Action<ILogger, string, string, Exception> _failedSendingPing =
            LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(26, "FailedSendingPing"), "Failed sending a ping message to service '{Endpoint}'. Id: {ServiceConnectionId}");

        private static readonly Action<ILogger, string, string, Exception> _receivedServiceErrorMessage =
            LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(27, "ReceivedServiceErrorMessage"), "Connection {ServiceConnectionId} received error message from service: {Error}");

        private static readonly Action<ILogger, string, Exception> _unexpectedExceptionInStop =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(29, "UnexpectedExceptionInStop"), "Connection {ServiceConnectionId} got unexpected exception in StopAsync.");

        private static readonly Action<ILogger, string, Exception> _onDemandConnectionHandshakeResponse =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(30, "OnDemandConnectionHandshakeResponse"), "Service returned handshake response: {Message}");

        private static readonly Action<ILogger, string, Exception> _receivedInstanceOfflinePing =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(31, "ReceivedInstanceOfflinePing"), "Received instance offline service ping: {InstanceId}");

        private static readonly Action<ILogger, string, string, Exception> _authorizeFailed =
            LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(32, "AuthorizeFailed"), "Service '{Endpoint}' returned 401 unauthorized. Authorization failed. Please check your role assignments. Note: New role assignments will take up to 30 minutes to take effect. Error detail: {Error}.");

        private static readonly Action<ILogger, string, string, Exception> _sendAccessKeyRequestMessageFailed =
            LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(33, "SendAccessKeyRequestFailed"), "Cannot send AccessKeyRequestMessage to '{Endpoint}' via server connections, authentication failures may occur if this warning continues. Error detail: {Message}");

        public static void FailedToWrite(ILogger logger, ulong? tracingId, string serviceConnectionId, Exception exception)
        {
            _failedToWrite(logger, tracingId, exception.Message, serviceConnectionId, null);
        }

        public static void AuthorizeFailed(ILogger logger, string endpoint, string message, Exception exception)
        {
            _authorizeFailed(logger, endpoint, message, exception);
        }

        public static void SendAccessKeyRequestFailed(ILogger logger, string endpoint, string message, Exception exception)
        {
            _sendAccessKeyRequestMessageFailed(logger, endpoint, message, exception);
        }

        public static void FailedToConnect(ILogger logger, string endpoint, string serviceConnectionId, Exception exception)
        {
            var message = exception.Message;
            var baseException = exception.GetBaseException();
            message += ". " + baseException.Message;

            _failedToConnect(logger, endpoint, message, serviceConnectionId, null);
        }

        public static void ErrorProcessingMessages(ILogger logger, string endpoint, string serviceConnectionId, Exception exception)
        {
            _errorProcessingMessages(logger, endpoint, serviceConnectionId, exception);
        }

        public static void ConnectionDropped(ILogger logger, string endpoint, string serviceConnectionId, Exception exception)
        {
            var message = exception.Message;
            var baseException = exception.GetBaseException();
            message += ". " + baseException.Message;

            _connectionDropped(logger, endpoint, serviceConnectionId, message, null);
        }

        public static void ServiceConnectionClosed(ILogger logger, string serviceConnectionId)
        {
            _serviceConnectionClosed(logger, serviceConnectionId, null);
        }

        public static void ServiceConnectionConnected(ILogger logger, string serviceConnectionId)
        {
            _serviceConnectionConnected(logger, serviceConnectionId, null);
        }

        public static void ReadingCancelled(ILogger logger, string serviceConnectionId)
        {
            _readingCancelled(logger, serviceConnectionId, null);
        }

        public static void ReceivedMessage(ILogger logger, long bytes, string serviceConnectionId)
        {
            _receivedMessage(logger, bytes, serviceConnectionId, null);
        }

        public static void StartingKeepAliveTimer(ILogger logger, TimeSpan keepAliveInterval)
        {
            _startingKeepAliveTimer(logger, keepAliveInterval.TotalMilliseconds, null);
        }

        public static void ServiceTimeout(ILogger logger, string endpoint, TimeSpan serviceTimeout, string serviceConnectionId)
        {
            _serviceTimeout(logger, endpoint, serviceTimeout.TotalMilliseconds, serviceConnectionId, null);
        }

        public static void SendingHandshakeRequest(ILogger logger)
        {
            _sendingHandshakeRequest(logger, null);
        }

        public static void HandshakeComplete(ILogger logger)
        {
            _handshakeComplete(logger, null);
        }

        public static void HandshakeError(ILogger logger, string endpoint, string error, string serviceConnectionId)
        {
            _handshakeError(logger, endpoint, error, serviceConnectionId, null);
        }

        public static void OnDemandConnectionHandshakeResponse(ILogger logger, string message)
        {
            _onDemandConnectionHandshakeResponse(logger, message, null);
        }

        public static void SentPing(ILogger logger)
        {
            _sentPing(logger, null);
        }

        public static void FailedSendingPing(ILogger logger, string endpoint, string serviceConnectionId, Exception exception)
        {
            _failedSendingPing(logger, endpoint, serviceConnectionId, exception);
        }

        public static void ReceivedServiceErrorMessage(ILogger logger, string connectionId, string errorMessage)
        {
            _receivedServiceErrorMessage(logger, connectionId, errorMessage, null);
        }

        public static void UnexpectedExceptionInStop(ILogger logger, string connectionId, Exception exception)
        {
            _unexpectedExceptionInStop(logger, connectionId, exception);
        }

        public static void ReceivedInstanceOfflinePing(ILogger logger, string instanceId)
        {
            _receivedInstanceOfflinePing(logger, instanceId, null);
        }
    }
}
