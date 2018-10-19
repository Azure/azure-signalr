// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceConnectionBase : IServiceConnection
    {
        private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);
        // Service ping rate is 15 sec; this is 2 times that.
        private static readonly TimeSpan DefaultServiceTimeout = TimeSpan.FromSeconds(30);
        private static readonly long DefaultServiceTimeoutTicks = DefaultServiceTimeout.Seconds * Stopwatch.Frequency;
        // App server ping rate is 5 sec. So service can detect an irresponsive server connection in 10 seconds at most.
        private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(5);
        private static readonly int MaxReconnectBackoffInternalInMilliseconds = 1000;

        // Start reconnect after a random interval less than 1 second
        private static TimeSpan ReconnectInterval =>
            TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackoffInternalInMilliseconds));

        private readonly ReadOnlyMemory<byte> _cachedPingBytes;
        private readonly HandshakeRequestMessage _handshakeRequest;

        private readonly SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);
        private readonly IServiceProtocol _serviceProtocol;

        private readonly TaskCompletionSource<bool> _serviceConnectionStartTcs = new TaskCompletionSource<bool>(TaskContinuationOptions.RunContinuationsAsynchronously);

        protected readonly ILogger _logger;
        protected readonly string _connectionId;

        private bool _isStopped;
        private long _lastReceiveTimestamp;
        private volatile bool _isConnected;
        protected ConnectionContext _connection;

        public Task WaitForConnectionStart => _serviceConnectionStartTcs.Task;

        public ServiceConnectionBase(IServiceProtocol serviceProtocol, ILogger logger, string connectionId)
        {
            _serviceProtocol = serviceProtocol;
            _logger = logger;
            _connectionId = connectionId;

            _cachedPingBytes = _serviceProtocol.GetMessageBytes(PingMessage.Instance);
            _handshakeRequest = new HandshakeRequestMessage(_serviceProtocol.Version);
        }

        public async Task StartAsync()
        {
            int retryCount = 0;
            while (!_isStopped)
            {
                // If we are not able to start, we will quit this connection.
                if (!await StartAsyncCore())
                {
                    _serviceConnectionStartTcs.TrySetResult(false);

                    await Task.Delay(GetRetryDelay(ref retryCount));
                    continue;
                }

                _serviceConnectionStartTcs.TrySetResult(true);
                retryCount = 0;
                _isConnected = true;
                await ProcessIncomingAsync();
                _isConnected = false;
            }
        }

        /// <summary>
        /// exponential back off with max 1 minute.
        /// </summary>
        public static TimeSpan GetRetryDelay(ref int retryCount)
        {
            // retry count:   0, 1, 2, 3, 4,  5,  6,  ...
            // delay seconds: 1, 2, 4, 8, 16, 32, 60, ...
            if (retryCount > 5)
            {
                return TimeSpan.FromMinutes(1) + ReconnectInterval;
            }
            return TimeSpan.FromSeconds(1 << retryCount++) + ReconnectInterval;
        }

        // For test purpose only
        public Task StopAsync()
        {
            _isStopped = true;
            _connection?.Transport.Input.CancelPendingRead();
            return Task.CompletedTask;
        }

        // For test purpose only
        public bool IsConnected => _isConnected;

        public async virtual Task WriteAsync(ServiceMessage serviceMessage)
        {
            // We have to lock around outgoing sends since the pipe is single writer.
            // The lock is per serviceConnection
            await _serviceConnectionLock.WaitAsync();

            if (_connection == null)
            {
                _serviceConnectionLock.Release();
                throw new InvalidOperationException("The connection is not active, data cannot be sent to the service.");
            }

            try
            {
                // Write the service protocol message
                _serviceProtocol.WriteMessage(serviceMessage, _connection.Transport.Output);
                await _connection.Transport.Output.FlushAsync();
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

        protected abstract Task<ConnectionContext> CreateConnection();

        protected abstract Task DisposeConnection();

        protected abstract Task CleanupConnections();

        protected abstract Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage);

        protected abstract Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage);

        protected abstract Task OnMessageAsync(ConnectionDataMessage connectionDataMessage);

        private async Task<bool> StartAsyncCore()
        {
            // Lock here in case somebody tries to send before the connection is assigned
            await _serviceConnectionLock.WaitAsync();

            try
            {
                _connection = await CreateConnection();

                if (await HandshakeAsync())
                {
                    Log.ServiceConnectionConnected(_logger, _connectionId);
                    return true;
                }
                else
                {
                    // False means we got a HandshakeResponseMessage with error. Will take below actions:
                    // - Dispose the connection
                    await DisposeConnection();

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.FailedToConnect(_logger, ex);

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
            await SendHandshakeRequestAsync(_connection.Transport.Output);

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    if (!Debugger.IsAttached)
                    {
                        cts.CancelAfter(DefaultHandshakeTimeout);
                    }

                    if (await ReceiveHandshakeResponseAsync(_connection.Transport.Input, cts.Token))
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

            _serviceProtocol.WriteMessage(_handshakeRequest, output);
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
                        if (_serviceProtocol.TryParseMessage(ref buffer, out var message))
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
                            Log.HandshakeError(_logger, handshakeResponse.ErrorMessage);
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
                    var result = await _connection.Transport.Input.ReadAsync();
                    var buffer = result.Buffer;

                    try
                    {
                        if (result.IsCanceled)
                        {
                            Log.ReadingCancelled(_logger, _connectionId);
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            Log.ReceivedMessage(_logger, buffer.Length, _connectionId);

                            UpdateReceiveTimestamp();

                            while (_serviceProtocol.TryParseMessage(ref buffer, out var message))
                            {
                                _ = DispatchMessageAsync(message);
                            }
                        }

                        if (result.IsCompleted)
                        {
                            // The connection is closed (reconnect)
                            Log.ServiceConnectionClosed(_logger, _connectionId);
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
                        _connection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fatal error: There is something wrong for the connection between SDK and service.
                // Abort all the client connections, close the httpConnection.
                // Only reconnect can recover.
                Log.ConnectionDropped(_logger, _connectionId, ex);
            }
            finally
            {
                keepAliveTimer.Stop();

                await _serviceConnectionLock.WaitAsync();
                try
                {
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
                case PingMessage _:
                    // ignore ping
                    break;
            }
            return Task.CompletedTask;
        }

        private TimerAwaitable StartKeepAliveTimer()
        {
            Log.StartingKeepAliveTimer(_logger, DefaultKeepAliveInterval);

            _lastReceiveTimestamp = Stopwatch.GetTimestamp();
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

                    // Send PingMessage to Service
                    await TrySendPingAsync();
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
                await _connection.Transport.Output.WriteAsync(_cachedPingBytes);
                Log.SentPing(_logger);
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
                if (_connection != null)
                {
                    _connection.Transport.Input.CancelPendingRead();
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
            private static readonly Action<ILogger, Exception> _failedToWrite =
                LoggerMessage.Define(LogLevel.Error, new EventId(1, "FailedToWrite"), "Failed to send message to the service.");

            private static readonly Action<ILogger, Exception> _failedToConnect =
                LoggerMessage.Define(LogLevel.Error, new EventId(2, "FailedToConnect"), "Failed to connect to the service.");

            private static readonly Action<ILogger, Exception> _errorProcessingMessages =
                LoggerMessage.Define(LogLevel.Error, new EventId(3, "ErrorProcessingMessages"), "Error when processing messages.");

            private static readonly Action<ILogger, string, Exception> _connectionDropped =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "ConnectionDropped"), "Connection {ServiceConnectionId} to the service was dropped.");

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

            public static void SentPing(ILogger logger)
            {
                _sentPing(logger, null);
            }

            public static void FailedSendingPing(ILogger logger, Exception exception)
            {
                _failedSendingPing(logger, exception);
            }
        }
    }
}
