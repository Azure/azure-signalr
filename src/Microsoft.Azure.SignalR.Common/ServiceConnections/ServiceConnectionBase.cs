// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceConnectionBase : IServiceConnection
    {
        private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);
        // Service ping rate is 5 sec to let server know service status. Set timeout for 30 sec for some space.
        private static readonly TimeSpan DefaultServiceTimeout = TimeSpan.FromSeconds(30);
        private static readonly long DefaultServiceTimeoutTicks = DefaultServiceTimeout.Seconds * Stopwatch.Frequency;
        // App server ping rate is 5 sec to let service know if app server is still alive
        // Service will abort both server and client connections link to this server when server is down.
        // App server ping is triggered by incoming requests and send by checking last send timestamp.
        private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(5);
        private static readonly long DefaultKeepAliveTicks = DefaultKeepAliveInterval.Seconds * Stopwatch.Frequency;

        private readonly ReadOnlyMemory<byte> _cachedPingBytes;
        private readonly HandshakeRequestMessage _handshakeRequest;

        private readonly SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);

        private readonly TaskCompletionSource<bool> _serviceConnectionStartTcs = new TaskCompletionSource<bool>(TaskContinuationOptions.RunContinuationsAsynchronously);
        private readonly ServerConnectionType _connectionType;

        private readonly IServiceMessageHandler _serviceMessageHandler;

        // Check service timeout
        private long _lastReceiveTimestamp;
        // Keep-alive tick
        private long _lastSendTimestamp;
        private volatile bool _isConnected;

        private readonly ILogger _logger;

        protected string ConnectionId { get; }

        protected IServiceProtocol ServiceProtocol { get; }

        protected ConnectionContext ConnectionContext { get; set; }

        protected string ErrorMessage { get; private set; }

        public ServiceConnectionStatus Status { get; private set; }

        public Task ConnectionInitializedTask => _serviceConnectionStartTcs.Task;

        public ServiceConnectionBase(IServiceProtocol serviceProtocol, ILoggerFactory loggerFactory, string connectionId, IServiceMessageHandler serviceMessageHandler, ServerConnectionType connectionType)
        {
            ServiceProtocol = serviceProtocol;
            ConnectionId = connectionId;

            _connectionType = connectionType;

            _cachedPingBytes = serviceProtocol.GetMessageBytes(PingMessage.Instance);
            _handshakeRequest = new HandshakeRequestMessage(serviceProtocol.Version, (int)connectionType);
            _logger = loggerFactory?.CreateLogger<ServiceConnectionBase>() ?? NullLogger<ServiceConnectionBase>.Instance;
            _serviceMessageHandler = serviceMessageHandler;
        }

        /// <summary>
        /// Start a service connection without the lifetime management.
        /// To get full lifetime management including dispose or restart, use <see cref="ServiceConnectionContainerBase"/>
        /// </summary>
        /// <param name="target">The target instance Id</param>
        /// <returns>The task of StartAsync</returns>
        public async Task StartAsync(string target = null)
        {
            // Codes in try block should catch and log exceptions separately.
            // The catch here should be very rare to reach.
            try
            {
                if (await StartAsyncCore(target))
                {
                    _serviceConnectionStartTcs.TrySetResult(true);
                    _isConnected = true;
                    await ProcessIncomingAsync();
                    _isConnected = false;
                }

                _serviceConnectionStartTcs.TrySetResult(false);
            }
            catch (Exception ex)
            {
                Status = ServiceConnectionStatus.Disconnected;
                _serviceConnectionStartTcs.TrySetException(ex);
                Log.UnexpectedExceptionInStart(_logger, ConnectionId, ex);
            }
            finally
            {
                await StopAsync();
            }
        }

        public Task StopAsync()
        {
            try
            {
                ConnectionContext?.Transport.Input.CancelPendingRead();
            }
            catch (Exception ex)
            {
                Log.UnexpectedExceptionInStop(_logger, ConnectionId, ex);
            }
            
            return Task.CompletedTask;
        }

        // For test purpose only
        public bool IsConnected => _isConnected;

        public async virtual Task WriteAsync(ServiceMessage serviceMessage)
        {
            // We have to lock around outgoing sends since the pipe is single writer.
            // The lock is per serviceConnection
            await _serviceConnectionLock.WaitAsync();

            var errorMessage = ErrorMessage;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                _serviceConnectionLock.Release();
                throw new InvalidOperationException(errorMessage);
            }

            if (ConnectionContext == null)
            {
                _serviceConnectionLock.Release();
                throw new ServiceConnectionNotActiveException();
            }

            try
            {
                // Write the service protocol message
                ServiceProtocol.WriteMessage(serviceMessage, ConnectionContext.Transport.Output);
                await ConnectionContext.Transport.Output.FlushAsync();
            }
            catch (Exception ex)
            {
                Log.FailedToWrite(_logger, ex);
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        protected abstract Task<ConnectionContext> CreateConnection(string target = null);

        protected abstract Task DisposeConnection();

        protected abstract Task CleanupConnections();

        protected abstract Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage);

        protected abstract Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage);

        protected abstract Task OnMessageAsync(ConnectionDataMessage connectionDataMessage);

        protected Task OnServiceErrorAsync(ServiceErrorMessage serviceErrorMessage)
        {
            if (!string.IsNullOrEmpty(serviceErrorMessage.ErrorMessage))
            {
                // When receives service error message, we suppose server -> service connection doesn't work,
                // and set ErrorMessage to prevent sending message from server to service
                // But messages in the pipe from service -> server should be processed as usual. Just log without
                // throw exception here.
                ErrorMessage = serviceErrorMessage.ErrorMessage;
                Log.ReceivedServiceErrorMessage(_logger, ConnectionId, serviceErrorMessage.ErrorMessage);
            }

            return Task.CompletedTask;
        }

        protected Task OnPingMessageAsync(PingMessage pingMessage)
        {
            return _serviceMessageHandler.HandlePingAsync(pingMessage);
        }

        private async Task<bool> StartAsyncCore(string target)
        {
            // Lock here in case somebody tries to send before the connection is assigned
            await _serviceConnectionLock.WaitAsync();

            try
            {
                Status = ServiceConnectionStatus.Connecting;
                ConnectionContext = await CreateConnection(target);
                ErrorMessage = null;

                if (await HandshakeAsync())
                {
                    Log.ServiceConnectionConnected(_logger, ConnectionId);
                    Status = ServiceConnectionStatus.Connected;
                    return true;
                }
                else
                {
                    // False means we got a HandshakeResponseMessage with error. Will take below actions:
                    // - Dispose the connection
                    Status = ServiceConnectionStatus.Disconnected;
                    await DisposeConnection();

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.FailedToConnect(_logger, ex);

                Status = ServiceConnectionStatus.Disconnected;
                await DisposeConnection();

                return false;
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        private async Task<bool> HandshakeAsync()
        {
            await SendHandshakeRequestAsync(ConnectionContext.Transport.Output);

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    if (!Debugger.IsAttached)
                    {
                        cts.CancelAfter(DefaultHandshakeTimeout);
                    }

                    if (await ReceiveHandshakeResponseAsync(ConnectionContext.Transport.Input, cts.Token))
                    {
                        Log.HandshakeComplete(_logger);
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorReceivingHandshakeResponse(_logger, ex);
                throw;
            }
        }

        private async Task SendHandshakeRequestAsync(PipeWriter output)
        {
            Log.SendingHandshakeRequest(_logger);

            ServiceProtocol.WriteMessage(_handshakeRequest, output);
            var sendHandshakeResult = await output.FlushAsync();
            if (sendHandshakeResult.IsCompleted)
            {
                throw new InvalidOperationException("Service disconnected before handshake complete.");
            }
        }

        private async Task<bool> ReceiveHandshakeResponseAsync(PipeReader input, CancellationToken token)
        {
            while (true)
            {
                var result = await input.ReadAsync(token);

                var buffer = result.Buffer;
                var consumed = buffer.Start;
                var examined = buffer.End;

                try
                {
                    if (result.IsCanceled)
                    {
                        throw new InvalidOperationException("Connection cancelled before handshake complete.");
                    }

                    if (!buffer.IsEmpty)
                    {
                        if (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                        {
                            consumed = buffer.Start;
                            examined = consumed;

                            if (!(message is HandshakeResponseMessage handshakeResponse))
                            {
                                throw new InvalidDataException(
                                    $"{message.GetType().Name} received when waiting for handshake response.");
                            }

                            if (string.IsNullOrEmpty(handshakeResponse.ErrorMessage))
                            {
                                return true;
                            }

                            // Handshake error. Will stop reconnect.
                            if (_connectionType == ServerConnectionType.OnDemand)
                            {
                                // Handshake errors on on-demand connections are acceptable.
                                Log.OnDemandConnectionHandshakeResponse(_logger, handshakeResponse.ErrorMessage);
                            }
                            else
                            {
                                Log.HandshakeError(_logger, handshakeResponse.ErrorMessage);
                            }
                            
                            return false;
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // Not enough data, and we won't be getting any more data.
                        throw new InvalidOperationException("Service disconnected before sending a handshake response.");
                    }
                }
                finally
                {
                    input.AdvanceTo(consumed, examined);
                }
            }
        }

        private async Task ProcessIncomingAsync()
        {
            var keepAliveTimer = StartKeepAliveTimer();
            try
            {
                while (true)
                {
                    var result = await ConnectionContext.Transport.Input.ReadAsync();
                    var buffer = result.Buffer;

                    try
                    {
                        if (result.IsCanceled)
                        {
                            Log.ReadingCancelled(_logger, ConnectionId);
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            Log.ReceivedMessage(_logger, buffer.Length, ConnectionId);

                            UpdateReceiveTimestamp();

                            // No matter what kind of message come in, trigger send ping check
                            _ = TrySendPingAsync();

                            while (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                            {
                                _ = DispatchMessageAsync(message);
                            }
                        }

                        if (result.IsCompleted)
                        {
                            // The connection is closed (reconnect)
                            Log.ServiceConnectionClosed(_logger, ConnectionId);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Error occurs in handling the message, but the connection between SDK and service still works.
                        // So, just log error instead of breaking the connection
                        Log.ErrorProcessingMessages(_logger, ex);
                    }
                    finally
                    {
                        ConnectionContext.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fatal error: There is something wrong for the connection between SDK and service.
                // Abort all the client connections, close the httpConnection.
                // Only reconnect can recover.
                Log.ConnectionDropped(_logger, ConnectionId, ex);
            }
            finally
            {
                keepAliveTimer.Stop();

                await _serviceConnectionLock.WaitAsync();
                try
                {
                    Status = ServiceConnectionStatus.Disconnected;
                    await DisposeConnection();
                }
                finally
                {
                    _serviceConnectionLock.Release();
                }
            }

            // TODO: Never cleanup connections unless Service asks us to do that
            // Current implementation is based on assumption that Service will drop clients
            // if server connection fails.
            await CleanupConnections();
        }

        private Task DispatchMessageAsync(ServiceMessage message)
        {
            switch (message)
            {
                case OpenConnectionMessage openConnectionMessage:
                    return OnConnectedAsync(openConnectionMessage);
                case CloseConnectionMessage closeConnectionMessage:
                    return OnDisconnectedAsync(closeConnectionMessage);
                case ConnectionDataMessage connectionDataMessage:
                    return OnMessageAsync(connectionDataMessage);
                case ServiceErrorMessage serviceErrorMessage:
                    return OnServiceErrorAsync(serviceErrorMessage);
                case PingMessage pingMessage:
                    return OnPingMessageAsync(pingMessage);
            }
            return Task.CompletedTask;
        }

        private TimerAwaitable StartKeepAliveTimer()
        {
            Log.StartingKeepAliveTimer(_logger, DefaultKeepAliveInterval);

            _lastReceiveTimestamp = Stopwatch.GetTimestamp();
            _lastSendTimestamp = _lastReceiveTimestamp;
            var timer = new TimerAwaitable(DefaultKeepAliveInterval, DefaultKeepAliveInterval);
            _ = KeepAliveAsync(timer);

            return timer;
        }

        private void UpdateReceiveTimestamp()
        {
            Interlocked.Exchange(ref _lastReceiveTimestamp, Stopwatch.GetTimestamp());
        }

        private async Task KeepAliveAsync(TimerAwaitable timer)
        {
            using (timer)
            {
                timer.Start();

                while (await timer)
                {
                    if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastReceiveTimestamp) > DefaultServiceTimeoutTicks)
                    {
                        AbortConnection();
                        // We shouldn't get here twice.
                        continue;
                    }
                }
            }
        }

        private async ValueTask TrySendPingAsync()
        {
            if (!_serviceConnectionLock.Wait(0))
            {
                // Skip sending PingMessage when failed getting lock
                return;
            }

            try
            {
                // Check if last send time is longer than default keep-alive ticks and then send ping
                if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSendTimestamp) > DefaultKeepAliveTicks)
                {
                    await ConnectionContext.Transport.Output.WriteAsync(GetPingMessage());
                    Interlocked.Exchange(ref _lastSendTimestamp, Stopwatch.GetTimestamp());
                    Log.SentPing(_logger);
                }
            }
            catch (Exception ex)
            {
                Log.FailedSendingPing(_logger, ex);
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        protected virtual ReadOnlyMemory<byte> GetPingMessage() => _cachedPingBytes;

        private void AbortConnection()
        {
            if (!_serviceConnectionLock.Wait(0))
            {
                // Couldn't get the lock so skip the cancellation (we could be in the middle of reconnecting?)
                return;
            }

            try
            {
                // Stop the reading from connection
                if (ConnectionContext != null)
                {
                    ConnectionContext.Transport.Input.CancelPendingRead();
                    Log.ServiceTimeout(_logger, DefaultServiceTimeout);
                }
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        private static class Log
        {
            // Category: ServiceConnection
            private static readonly Action<ILogger, string, Exception> _failedToWrite =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "FailedToWrite"), "Failed to send message to the service: {message}");

            private static readonly Action<ILogger, string, Exception> _failedToConnect =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, "FailedToConnect"), "Failed to connect to the service, will retry after the back off period. Error detail: {message}.");

            private static readonly Action<ILogger, Exception> _errorProcessingMessages =
                LoggerMessage.Define(LogLevel.Error, new EventId(3, "ErrorProcessingMessages"), "Error when processing messages.");

            private static readonly Action<ILogger, string, Exception> _connectionDropped =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "ConnectionDropped"), "Connection to the service was dropped, probably caused by network instability or service restart. Will try to reconnect after the back off period. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, Exception> _failedToCleanupConnections =
                LoggerMessage.Define(LogLevel.Error, new EventId(5, "FailedToCleanupConnection"), "Failed to clean up client connections.");

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

            private static readonly Action<ILogger, string, Exception> _readingCancelled =
                LoggerMessage.Define<string>(LogLevel.Trace, new EventId(15, "ReadingCancelled"), "Reading from service connection {ServiceConnectionId} cancelled.");

            private static readonly Action<ILogger, long, string, Exception> _receivedMessage =
                LoggerMessage.Define<long, string>(LogLevel.Debug, new EventId(16, "ReceivedMessage"), "Received {ReceivedBytes} bytes from service {ServiceConnectionId}.");

            private static readonly Action<ILogger, double, Exception> _startingKeepAliveTimer =
                LoggerMessage.Define<double>(LogLevel.Trace, new EventId(17, "StartingKeepAliveTimer"), "Starting keep-alive timer. Duration: {KeepAliveInterval:0.00}ms");

            private static readonly Action<ILogger, double, Exception> _serviceTimeout =
                LoggerMessage.Define<double>(LogLevel.Error, new EventId(18, "ServiceTimeout"), "Service timeout. {ServiceTimeout:0.00}ms elapsed without receiving a message from service.");

            private static readonly Action<ILogger, long, string, Exception> _writeMessageToApplication =
                LoggerMessage.Define<long, string>(LogLevel.Trace, new EventId(19, "WriteMessageToApplication"), "Writing {ReceivedBytes} to connection {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _serviceConnectionConnected =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(20, "ServiceConnectionConnected"), "Service connection {ServiceConnectionId} connected.");

            private static readonly Action<ILogger, Exception> _sendingHandshakeRequest =
                LoggerMessage.Define(LogLevel.Debug, new EventId(21, "SendingHandshakeRequest"), "Sending Handshake request to service.");

            private static readonly Action<ILogger, Exception> _handshakeComplete =
                LoggerMessage.Define(LogLevel.Debug, new EventId(22, "HandshakeComplete"), "Handshake with service completes.");

            private static readonly Action<ILogger, Exception> _errorReceivingHandshakeResponse =
                LoggerMessage.Define(LogLevel.Error, new EventId(23, "ErrorReceivingHandshakeResponse"), "Error receiving handshake response.");

            private static readonly Action<ILogger, string, Exception> _handshakeError =
                LoggerMessage.Define<string>(LogLevel.Critical, new EventId(24, "HandshakeError"), "Service returned handshake error: {Error}");

            private static readonly Action<ILogger, Exception> _sentPing =
                LoggerMessage.Define(LogLevel.Debug, new EventId(25, "SentPing"), "Sent a ping message to service.");

            private static readonly Action<ILogger, Exception> _failedSendingPing =
                LoggerMessage.Define(LogLevel.Warning, new EventId(26, "FailedSendingPing"), "Failed sending a ping message to service.");

            private static readonly Action<ILogger, string, string, Exception> _receivedServiceErrorMessage =
                LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(27, "ReceivedServiceErrorMessage"), "Connection {ServiceConnectionId} received error message from service: {Error}");

            private static readonly Action<ILogger, string, Exception> _unexpectedExceptionInStart =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(28, "UnexpectedExceptionInStart"), "Connection {ServiceConnectionId} got unexpected exception in StarAsync.");

            private static readonly Action<ILogger, string, Exception> _unexpectedExceptionInStop =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(29, "UnexpectedExceptionInStop"), "Connection {ServiceConnectionId} got unexpected exception in StopAsync.");

            private static readonly Action<ILogger, string, Exception> _onDemandConnectionHandshakeResponse =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(30, "OnDemandConnectionHandshakeResponse"), "Service returned handshake response: {Message}");

            public static void FailedToWrite(ILogger logger, Exception exception)
            {
                _failedToWrite(logger, exception.Message, null);
            }

            public static void FailedToConnect(ILogger logger, Exception exception)
            {
                var message = exception.Message;
                var baseException = exception.GetBaseException();
                message += ". " + baseException.Message;

                _failedToConnect(logger, message, null);
            }

            public static void ErrorProcessingMessages(ILogger logger, Exception exception)
            {
                _errorProcessingMessages(logger, exception);
            }

            public static void ConnectionDropped(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _connectionDropped(logger, serviceConnectionId, exception);
            }

            public static void FailedToCleanupConnections(ILogger logger, Exception exception)
            {
                _failedToCleanupConnections(logger, exception);
            }

            public static void ErrorSendingMessage(ILogger logger, Exception exception)
            {
                _errorSendingMessage(logger, exception);
            }

            public static void SendLoopStopped(ILogger logger, string connectionId, Exception exception)
            {
                _sendLoopStopped(logger, connectionId, exception);
            }

            public static void ApplicationTaskFailed(ILogger logger, Exception exception)
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

            public static void ServiceTimeout(ILogger logger, TimeSpan serviceTimeout)
            {
                _serviceTimeout(logger, serviceTimeout.TotalMilliseconds, null);
            }

            public static void WriteMessageToApplication(ILogger logger, long count, string connectionId)
            {
                _writeMessageToApplication(logger, count, connectionId, null);
            }

            public static void SendingHandshakeRequest(ILogger logger)
            {
                _sendingHandshakeRequest(logger, null);
            }

            public static void HandshakeComplete(ILogger logger)
            {
                _handshakeComplete(logger, null);
            }

            public static void ErrorReceivingHandshakeResponse(ILogger logger, Exception exception)
            {
                _errorReceivingHandshakeResponse(logger, exception);
            }

            public static void HandshakeError(ILogger logger, string error)
            {
                _handshakeError(logger, error, null);
            }

            public static void OnDemandConnectionHandshakeResponse(ILogger logger, string message)
            {
                _onDemandConnectionHandshakeResponse(logger, message, null);
            }

            public static void SentPing(ILogger logger)
            {
                _sentPing(logger, null);
            }

            public static void FailedSendingPing(ILogger logger, Exception exception)
            {
                _failedSendingPing(logger, exception);
            }

            public static void ReceivedServiceErrorMessage(ILogger logger, string connectionId, string errorMessage)
            {
                _receivedServiceErrorMessage(logger, connectionId, errorMessage, null);
            }

            public static void UnexpectedExceptionInStart(ILogger logger, string connectionId, Exception exception)
            {
                _unexpectedExceptionInStart(logger, connectionId, exception);
            }

            public static void UnexpectedExceptionInStop(ILogger logger, string connectionId, Exception exception)
            {
                _unexpectedExceptionInStop(logger, connectionId, exception);
            }
        }
    }
}
