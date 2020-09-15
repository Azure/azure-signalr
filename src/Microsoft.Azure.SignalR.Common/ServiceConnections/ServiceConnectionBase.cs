﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceConnectionBase : IServiceConnection
    {
        protected static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);
        // Service ping rate is 5 sec to let server know service status. Set timeout for 30 sec for some space.
        private static readonly TimeSpan DefaultServiceTimeout = TimeSpan.FromSeconds(30);
        private static readonly long DefaultServiceTimeoutTicks = (long)(DefaultServiceTimeout.TotalSeconds * Stopwatch.Frequency);
        // App server ping rate is 5 sec to let service know if app server is still alive
        // Service will abort both server and client connections link to this server when server is down.
        // App server ping is triggered by incoming requests and send by checking last send timestamp.
        private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(5);
        private static readonly long DefaultKeepAliveTicks = (long)DefaultKeepAliveInterval.TotalSeconds * Stopwatch.Frequency;

        private readonly ReadOnlyMemory<byte> _cachedPingBytes;

        private readonly HandshakeRequestMessage _handshakeRequest;

        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        private readonly TaskCompletionSource<bool> _serviceConnectionStartTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object> _serviceConnectionOfflineTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly ServiceConnectionType _connectionType;

        private readonly IServiceMessageHandler _serviceMessageHandler;
        private readonly object _statusLock = new object();

        private volatile string _errorMessage;

        // Check service timeout
        private long _lastReceiveTimestamp;

        // Keep-alive tick
        private long _lastSendTimestamp;

        private ServiceConnectionStatus _status;

        private int _started;

        protected HubServiceEndpoint HubEndpoint { get; }

        protected string ServerId { get; }

        protected string ConnectionId { get; }

        protected ILogger Logger { get; }

        protected IServiceProtocol ServiceProtocol { get; }

        private ConnectionContext _connectionContext;

        public event Action<StatusChange> ConnectionStatusChanged;

        public ServiceConnectionStatus Status
        {
            get => _status;
            protected set
            {
                if (_status != value)
                {
                    lock (_statusLock)
                    {
                        if (_status != value)
                        {
                            var prev = _status;
                            _status = value;
                            ConnectionStatusChanged?.Invoke(new StatusChange(prev, value));
                        }
                    }
                }
            }
        }

        public Task ConnectionInitializedTask => _serviceConnectionStartTcs.Task;

        public Task ConnectionOfflineTask => _serviceConnectionOfflineTcs.Task;

        protected ServiceConnectionBase(
            IServiceProtocol serviceProtocol,
            string serverId,
            string connectionId,
            HubServiceEndpoint endpoint,
            IServiceMessageHandler serviceMessageHandler,
            ServiceConnectionType connectionType,
            ILogger logger,
            GracefulShutdownMode mode = GracefulShutdownMode.Off)
        {
            ServiceProtocol = serviceProtocol;
            ServerId = serverId;
            ConnectionId = connectionId;

            _connectionType = connectionType;
            HubEndpoint = endpoint;

            if (serviceProtocol != null)
            {
                _cachedPingBytes = serviceProtocol.GetMessageBytes(PingMessage.Instance);

                var migrationLevel = mode == GracefulShutdownMode.MigrateClients ? 1 : 0;
                _handshakeRequest = new HandshakeRequestMessage(serviceProtocol.Version, (int)connectionType, migrationLevel);
            }

            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            {
                throw new InvalidOperationException("Connection already started!");
            }

            Status = ServiceConnectionStatus.Connecting;

            var connection = await EstablishConnectionAsync(target);
            if (connection != null)
            {
                _connectionContext = connection;
                Status = ServiceConnectionStatus.Connected;
                _serviceConnectionStartTcs.TrySetResult(true);
                try
                {
                    try
                    {
                        await ProcessIncomingAsync(connection);
                    }
                    finally
                    {
                        // when ProcessIncoming completes, clean up the connection

                        // TODO: Never cleanup connections unless Service asks us to do that
                        // Current implementation is based on assumption that Service will drop clients
                        // if server connection fails.
                        await CleanupClientConnections();
                    }
                }
                catch (Exception ex)
                {
                    Log.ConnectionDropped(Logger, HubEndpoint.ToString(), ConnectionId, ex);
                }
                finally
                {
                    // wait until all the connections are cleaned up to close the outgoing pipe
                    // mark the status as Disconnected so that no one will write to this connection anymore
                    // Don't allow write anymore when the connection is disconnected
                    Status = ServiceConnectionStatus.Disconnected;

                    await _writeLock.WaitAsync();
                    try
                    {
                        // close the underlying connection
                        await DisposeConnection(connection);
                    }
                    finally
                    {
                        _writeLock.Release();
                    }
                }
            }
            else
            {
                Status = ServiceConnectionStatus.Disconnected;
                _serviceConnectionStartTcs.TrySetResult(false);
            }
        }

        public Task StopAsync()
        {
            try
            {
                _connectionContext?.Transport.Input.CancelPendingRead();
            }
            catch (Exception ex)
            {
                Log.UnexpectedExceptionInStop(Logger, ConnectionId, ex);
            }
            return Task.CompletedTask;
        }

        public async Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (!await SafeWriteAsync(serviceMessage))
            {
                throw new ServiceConnectionNotActiveException(_errorMessage);
            }
        }

        protected virtual async Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
        {
            if (!string.IsNullOrEmpty(_errorMessage) || Status != ServiceConnectionStatus.Connected)
            {
                return false;
            }

            await _writeLock.WaitAsync();

            if (Status != ServiceConnectionStatus.Connected)
            {
                // Make sure not write messages to the connection when it is no longer connected
                _writeLock.Release();
                return false;
            }
            try
            {
                // Write the service protocol message
                ServiceProtocol.WriteMessage(serviceMessage, _connectionContext.Transport.Output);
                await _connectionContext.Transport.Output.FlushAsync();
                return true;
            }
            catch (Exception ex)
            {
                // We always mark the connection as Disconnected before dispose the underlying http connection
                // So in theory this log should never trigger
                Log.FailedToWrite(Logger, (serviceMessage as IMessageWithTracingId)?.TracingId, ConnectionId, ex);
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        protected abstract Task<ConnectionContext> CreateConnection(string target = null);

        protected abstract Task DisposeConnection(ConnectionContext connection);

        protected abstract Task CleanupClientConnections(string fromInstanceId = null);

        protected abstract Task OnClientConnectedAsync(OpenConnectionMessage openConnectionMessage);

        protected abstract Task OnClientDisconnectedAsync(CloseConnectionMessage closeConnectionMessage);

        protected abstract Task OnClientMessageAsync(ConnectionDataMessage connectionDataMessage);

        protected Task OnServiceErrorAsync(ServiceErrorMessage serviceErrorMessage)
        {
            if (!string.IsNullOrEmpty(serviceErrorMessage.ErrorMessage))
            {
                // When receives service error message, we suppose server -> service connection doesn't work,
                // and set _errorMessage to prevent sending message from server to service
                // But messages in the pipe from service -> server should be processed as usual. Just log without
                // throw exception here.
                _errorMessage = serviceErrorMessage.ErrorMessage;

                // Update the status immediately
                Status = ServiceConnectionStatus.Disconnected;
                Log.ReceivedServiceErrorMessage(Logger, ConnectionId, serviceErrorMessage.ErrorMessage);
            }

            return Task.CompletedTask;
        }

        protected Task OnPingMessageAsync(PingMessage pingMessage)
        {
            if (RuntimeServicePingMessage.TryGetOffline(pingMessage, out var instanceId))
            {
                Log.ReceivedInstanceOfflinePing(Logger, instanceId);
                return CleanupClientConnections(instanceId);
            } 
            if (RuntimeServicePingMessage.IsFinAck(pingMessage))
            {
                _serviceConnectionOfflineTcs.TrySetResult(null);
                return Task.CompletedTask;
            }
            return _serviceMessageHandler.HandlePingAsync(pingMessage);
        }

        protected Task OnAckMessageAsync(AckMessage ackMessage)
        {
            _serviceMessageHandler.HandleAck(ackMessage);
            return Task.CompletedTask;
        }

        private async Task<ConnectionContext> EstablishConnectionAsync(string target)
        {
            try
            {
                var connectionContext = await CreateConnection(target);
                try
                {
                    if (await HandshakeAsync(connectionContext))
                    {
                        Log.ServiceConnectionConnected(Logger, ConnectionId);
                        return connectionContext;
                    }
                }
                catch (Exception ex)
                {
                    Log.HandshakeError(Logger, ex.Message, ConnectionId);
                    await DisposeConnection(connectionContext);
                    return null;
                }

                // handshake return false
                await DisposeConnection(connectionContext);

                return null;
            }
            catch (Exception ex)
            {
                if (target == null)
                {
                    // Log for required connections only to reduce noise for rebalance
                    // connection failure usually due to service maintenance.
                    Log.FailedToConnect(Logger, HubEndpoint.ToString(), ConnectionId, ex);
                }
                return null;
            }
        }

        protected virtual async Task<bool> HandshakeAsync(ConnectionContext context)
        {
            await SendHandshakeRequestAsync(context.Transport.Output);

            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    if (!Debugger.IsAttached)
                    {
                        cts.CancelAfter(DefaultHandshakeTimeout);
                    }

                    if (await ReceiveHandshakeResponseAsync(context.Transport.Input, cts.Token))
                    {
                        Log.HandshakeComplete(Logger);
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorReceivingHandshakeResponse(Logger, ConnectionId, ex);
                throw;
            }
        }

        private async Task SendHandshakeRequestAsync(PipeWriter output)
        {
            Log.SendingHandshakeRequest(Logger);

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
                            if (_connectionType == ServiceConnectionType.OnDemand)
                            {
                                // Handshake errors on on-demand connections are acceptable.
                                Log.OnDemandConnectionHandshakeResponse(Logger, handshakeResponse.ErrorMessage);
                            }
                            else
                            {
                                Log.HandshakeError(Logger, handshakeResponse.ErrorMessage, ConnectionId);
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

        private async Task ProcessIncomingAsync(ConnectionContext connection)
        {
            var keepAliveTimer = StartKeepAliveTimer();

            try
            {
                while (true)
                {
                    var result = await connection.Transport.Input.ReadAsync();
                    var buffer = result.Buffer;

                    try
                    {
                        if (result.IsCanceled)
                        {
                            Log.ReadingCancelled(Logger, ConnectionId);
                            break;
                        }

                        if (!buffer.IsEmpty)
                        {
                            Log.ReceivedMessage(Logger, buffer.Length, ConnectionId);

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
                            Log.ServiceConnectionClosed(Logger, ConnectionId);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Error occurs in handling the message, but the connection between SDK and service still works.
                        // So, just log error instead of breaking the connection
                        Log.ErrorProcessingMessages(Logger, ConnectionId, ex);
                    }
                    finally
                    {
                        connection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            finally
            {
                keepAliveTimer.Stop();
                _serviceConnectionOfflineTcs.TrySetResult(true);
            }
        }

        private Task DispatchMessageAsync(ServiceMessage message)
        {
            switch (message)
            {
                case OpenConnectionMessage openConnectionMessage:
                    return OnClientConnectedAsync(openConnectionMessage);
                case CloseConnectionMessage closeConnectionMessage:
                    return OnClientDisconnectedAsync(closeConnectionMessage);
                case ConnectionDataMessage connectionDataMessage:
                    return OnClientMessageAsync(connectionDataMessage);
                case ServiceErrorMessage serviceErrorMessage:
                    return OnServiceErrorAsync(serviceErrorMessage);
                case PingMessage pingMessage:
                    return OnPingMessageAsync(pingMessage);
                case AckMessage ackMessage:
                    return OnAckMessageAsync(ackMessage);
            }
            return Task.CompletedTask;
        }

        private TimerAwaitable StartKeepAliveTimer()
        {
            Log.StartingKeepAliveTimer(Logger, DefaultKeepAliveInterval);

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
                        Log.ServiceTimeout(Logger, DefaultServiceTimeout, ConnectionId);
                        await StopAsync();
                        // We shouldn't get here twice.
                        continue;
                    }
                }
            }
        }

        private async ValueTask TrySendPingAsync()
        {
            if (!_writeLock.Wait(0))
            {
                // Skip sending PingMessage when failed getting lock
                return;
            }

            try
            {
                // Check if last send time is longer than default keep-alive ticks and then send ping
                if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSendTimestamp) > DefaultKeepAliveTicks)
                {
                    await _connectionContext.Transport.Output.WriteAsync(GetPingMessage());
                    Interlocked.Exchange(ref _lastSendTimestamp, Stopwatch.GetTimestamp());
                    Log.SentPing(Logger);
                }
            }
            catch (Exception ex)
            {
                Log.FailedSendingPing(Logger, ConnectionId, ex);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        protected virtual ReadOnlyMemory<byte> GetPingMessage() => _cachedPingBytes;

        private static class Log
        {
            // Category: ServiceConnection
            private static readonly Action<ILogger, ulong?, string, string, Exception> _failedToWrite =
                LoggerMessage.Define<ulong?, string, string>(LogLevel.Error, new EventId(1, "FailedToWrite"), "Failed to send message {tracingId} to the service: {message}. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, string, string, Exception> _failedToConnect =
                LoggerMessage.Define<string, string, string>(LogLevel.Error, new EventId(2, "FailedToConnect"), "Failed to connect to '{endpoint}', will retry after the back off period. Error detail: {message}. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, Exception> _errorProcessingMessages =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "ErrorProcessingMessages"), "Error when processing messages. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, string, string, Exception> _connectionDropped =
                LoggerMessage.Define<string, string, string>(LogLevel.Error, new EventId(4, "ConnectionDropped"), "Connection to '{endpoint}' was dropped, probably caused by network instability or service restart. Will try to reconnect after the back off period. Error detail: {message}. Id: {ServiceConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _serviceConnectionClosed =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(14, "serviceConnectionClose"), "Service connection {ServiceConnectionId} closed.");

            private static readonly Action<ILogger, string, Exception> _readingCancelled =
                LoggerMessage.Define<string>(LogLevel.Trace, new EventId(15, "ReadingCancelled"), "Reading from service connection {ServiceConnectionId} cancelled.");

            private static readonly Action<ILogger, long, string, Exception> _receivedMessage =
                LoggerMessage.Define<long, string>(LogLevel.Debug, new EventId(16, "ReceivedMessage"), "Received {ReceivedBytes} bytes from service {ServiceConnectionId}.");

            private static readonly Action<ILogger, double, Exception> _startingKeepAliveTimer =
                LoggerMessage.Define<double>(LogLevel.Trace, new EventId(17, "StartingKeepAliveTimer"), "Starting keep-alive timer. Duration: {KeepAliveInterval:0.00}ms");

            private static readonly Action<ILogger, double, string, Exception> _serviceTimeout =
                LoggerMessage.Define<double, string>(LogLevel.Error, new EventId(18, "ServiceTimeout"), "Service timeout. {ServiceTimeout:0.00}ms elapsed without receiving a message from service. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, long, string, Exception> _writeMessageToApplication =
                LoggerMessage.Define<long, string>(LogLevel.Trace, new EventId(19, "WriteMessageToApplication"), "Writing {ReceivedBytes} to connection {TransportConnectionId}.");

            private static readonly Action<ILogger, string, Exception> _serviceConnectionConnected =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(20, "ServiceConnectionConnected"), "Service connection {ServiceConnectionId} connected.");

            private static readonly Action<ILogger, Exception> _sendingHandshakeRequest =
                LoggerMessage.Define(LogLevel.Debug, new EventId(21, "SendingHandshakeRequest"), "Sending Handshake request to service.");

            private static readonly Action<ILogger, Exception> _handshakeComplete =
                LoggerMessage.Define(LogLevel.Debug, new EventId(22, "HandshakeComplete"), "Handshake with service completes.");

            private static readonly Action<ILogger, string, Exception> _errorReceivingHandshakeResponse =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(23, "ErrorReceivingHandshakeResponse"), "Error receiving handshake response. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, string, Exception> _handshakeError =
                LoggerMessage.Define<string, string>(LogLevel.Critical, new EventId(24, "HandshakeError"), "Service returned handshake error: {Error}. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, Exception> _sentPing =
                LoggerMessage.Define(LogLevel.Debug, new EventId(25, "SentPing"), "Sent a ping message to service.");

            private static readonly Action<ILogger, string, Exception> _failedSendingPing =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(26, "FailedSendingPing"), "Failed sending a ping message to service. Id: {ServiceConnectionId}");

            private static readonly Action<ILogger, string, string, Exception> _receivedServiceErrorMessage =
                LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(27, "ReceivedServiceErrorMessage"), "Connection {ServiceConnectionId} received error message from service: {Error}");

            private static readonly Action<ILogger, string, Exception> _unexpectedExceptionInStart =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(28, "UnexpectedExceptionInStart"), "Connection {ServiceConnectionId} got unexpected exception in StarAsync.");

            private static readonly Action<ILogger, string, Exception> _unexpectedExceptionInStop =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(29, "UnexpectedExceptionInStop"), "Connection {ServiceConnectionId} got unexpected exception in StopAsync.");

            private static readonly Action<ILogger, string, Exception> _onDemandConnectionHandshakeResponse =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(30, "OnDemandConnectionHandshakeResponse"), "Service returned handshake response: {Message}");

            private static readonly Action<ILogger, string, Exception> _receivedInstanceOfflinePing =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(31, "ReceivedInstanceOfflinePing"), "Received instance offline service ping: {InstanceId}");

            public static void FailedToWrite(ILogger logger, ulong? tracingId, string serviceConnectionId, Exception exception)
            {
                _failedToWrite(logger, tracingId, exception.Message, serviceConnectionId, null);
            }

            public static void FailedToConnect(ILogger logger, string endpoint, string serviceConnectionId, Exception exception)
            {
                var message = exception.Message;
                var baseException = exception.GetBaseException();
                message += ". " + baseException.Message;

                _failedToConnect(logger, endpoint, message, serviceConnectionId, null);
            }

            public static void ErrorProcessingMessages(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _errorProcessingMessages(logger, serviceConnectionId, exception);
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

            public static void ServiceTimeout(ILogger logger, TimeSpan serviceTimeout, string serviceConnectionId)
            {
                _serviceTimeout(logger, serviceTimeout.TotalMilliseconds, serviceConnectionId, null);
            }

            public static void SendingHandshakeRequest(ILogger logger)
            {
                _sendingHandshakeRequest(logger, null);
            }

            public static void HandshakeComplete(ILogger logger)
            {
                _handshakeComplete(logger, null);
            }

            public static void ErrorReceivingHandshakeResponse(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _errorReceivingHandshakeResponse(logger, serviceConnectionId, exception);
            }

            public static void HandshakeError(ILogger logger, string error, string serviceConnectionId)
            {
                _handshakeError(logger, error, serviceConnectionId, null);
            }

            public static void OnDemandConnectionHandshakeResponse(ILogger logger, string message)
            {
                _onDemandConnectionHandshakeResponse(logger, message, null);
            }

            public static void SentPing(ILogger logger)
            {
                _sentPing(logger, null);
            }

            public static void FailedSendingPing(ILogger logger, string serviceConnectionId, Exception exception)
            {
                _failedSendingPing(logger, serviceConnectionId, exception);
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

            public static void ReceivedInstanceOfflinePing(ILogger logger, string instanceId)
            {
                _receivedInstanceOfflinePing(logger, instanceId, null);
            }
        }
    }
}
