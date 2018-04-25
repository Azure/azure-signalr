// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnection
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);
        private readonly ILogger<ServiceConnection> _logger;
        // Server ping rate is 15 sec, this is 2 times that.
        private readonly TimeSpan DefaultServerTimeout = TimeSpan.FromSeconds(30);
        private readonly ConcurrentDictionary<string, string> _connectionIds = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private ConnectionContext _connection;
        private ConnectionDelegate _connectionDelegate;
        // Start reconnect after a random interval less than 1 second
        private TimeSpan ReconnectInterval => TimeSpan.FromMilliseconds(StaticRandom.Next(1000));

        public ServiceConnection(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory, ILoggerFactory loggerFactory,
            ConnectionDelegate connectionDelegate)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _connectionDelegate = connectionDelegate;
            _logger = loggerFactory.CreateLogger<ServiceConnection>();
        }

        public async Task StartAsync()
        {
            while (true)
            {
                await StartAsyncCore();

                await ProcessIncomingAsync();
            }
        }

        public async Task WriteAsync(ServiceMessage serviceMessage)
        {
            // We have to lock around outgoing sends since the pipe is single writer.
            // The lock is per serviceConnection
            await _serviceConnectionLock.WaitAsync();

            if (_connection == null)
            {
                _serviceConnectionLock.Release();
                throw new InvalidOperationException("The connection is not active, data cannot be sent to the service");
            }

            try
            {
                // Write the service protocol message
                _serviceProtocol.WriteMessage(serviceMessage, _connection.Transport.Output);
                await _connection.Transport.Output.FlushAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.FailToWriteAsync(_logger, ex);
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        private async Task StartAsyncCore()
        {
            // Always try until connected
            while (true)
            {
                // Lock here in case somebody tries to send before the connection is assigned
                await _serviceConnectionLock.WaitAsync();

                try
                {
                    _connection = await _connectionFactory.ConnectAsync(TransferFormat.Binary);
                    return;
                }
                catch (Exception ex)
                {
                    Log.FailToConnect(_logger, ex);
                    await Task.Delay(ReconnectInterval);
                }
                finally
                {
                    _serviceConnectionLock.Release();
                }
            }
        }

        private Timer StartTimeoutTimer()
        {
            Log.StartingServerTimeoutTimer(_logger, DefaultServerTimeout);
            return new Timer(state => ((ServiceConnection)state).TimeoutElapsed(),
                    this, Timeout.Infinite, Timeout.Infinite);
        }

        private void ResetTimeoutTimer(Timer timeoutTimer)
        {
            Log.ResettingKeepAliveTimer(_logger);
            timeoutTimer.Change(DefaultServerTimeout, Timeout.InfiniteTimeSpan);
        }

        private void TimeoutElapsed()
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
                    Log.ServerTimeout(_logger, DefaultServerTimeout);
                }
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        private async Task ProcessIncomingAsync()
        {
            var timeoutTimer = StartTimeoutTimer();
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
                            Log.ReadingCanceled(_logger);
                            break;
                        }
                        if (!buffer.IsEmpty)
                        {
                            ResetTimeoutTimer(timeoutTimer);
                            Log.ReceivedRequest(_logger);
                            while (_serviceProtocol.TryParseMessage(ref buffer, out ServiceMessage message))
                            {
                                _ = DispatchMessage(message);
                            }
                        }
                        else if (result.IsCompleted)
                        {
                            // The connection is closed (reconnect)
                            Log.ConnectionClosed(_logger);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Error occurs in handling the message, but the connection between SDK and service still works.
                        // So, just log error instead of breaking the connection
                        Log.ErrorProcessingRequest(_logger, ex);
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
                Log.ConnectionDropped(_logger, ex);
            }
            finally
            {
                timeoutTimer.Dispose();
                await _connectionFactory.DisposeAsync(_connection);
            }

            await _serviceConnectionLock.WaitAsync();
            try
            {
                _connection = null;
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
            // TODO: Never cleanup connections unless Service asks us to do that
            // Current implementation is based on assumption that Service will drop clients
            // if server connection fails.
            await CleanupConnections();
        }

        private async Task CleanupConnections()
        {
            try
            {
                if (_connectionIds.Count == 0)
                {
                    return;
                }
                var tasks = new List<Task>(_connectionIds.Count);
                foreach (var connectionId in _connectionIds.Keys)
                {
                    tasks.Add(PerformDisconnectAsync(connectionId));
                }
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Log.FailToCleanupConnections(_logger, ex);
            }
        }

        private async Task DispatchMessage(ServiceMessage message)
        {
            switch (message)
            {
                case OpenConnectionMessage openConnectionMessage:
                    await OnConnectedAsync(openConnectionMessage);
                    break;
                case CloseConnectionMessage closeConnectionMessage:
                    await OnDisconnectedAsync(closeConnectionMessage);
                    break;
                case ConnectionDataMessage connectionDataMessage:
                    await OnMessageAsync(connectionDataMessage);
                    break;
                case PingMessage _:
                    // ignore ping
                    break;
            }
        }

        private async Task ProcessOutgoingMessagesAsync(ServiceConnectionContext connection)
        {
            try
            {
                while (true)
                {
                    var result = await connection.Application.Input.ReadAsync();
                    var buffer = result.Buffer;
                    if (!buffer.IsEmpty)
                    {
                        try
                        {
                            // Forward the message to the service
                            if (buffer.IsSingleSegment)
                            {
                                await WriteAsync(new ConnectionDataMessage(connection.ConnectionId, buffer.First));
                            }
                            else
                            {
                                // This is a multi-segmented buffer so just write each chunk
                                // TODO: Optimize this by doing it all under a single lock
                                var position = buffer.Start;
                                while (buffer.TryGet(ref position, out var memory))
                                {
                                    await WriteAsync(new ConnectionDataMessage(connection.ConnectionId, memory));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorSendingMessage(_logger, ex);
                        }
                    }
                    else if (result.IsCompleted)
                    {
                        // This connection ended (the application itself shut down) we should remove it from the list of connections
                        break;
                    }
                    connection.Application.Input.AdvanceTo(buffer.End);
                }
            }
            catch (Exception ex)
            {
                Log.SendLoopStopped(_logger, ex);
            }
            finally
            {
                connection.Application.Input.Complete();
            }
        }

        private void AddClientConnection(ServiceConnectionContext connection)
        {
            _clientConnectionManager.AddClientConnection(connection);
            _connectionIds.TryAdd(connection.ConnectionId, connection.ConnectionId);
        }

        private void RemoveClientConnection(string connectionId)
        {
            _clientConnectionManager.ClientConnections.TryRemove(connectionId, out _);
            _connectionIds.TryRemove(connectionId, out _);
        }

        private Task OnConnectedAsync(OpenConnectionMessage message)
        {
            var connection = new ServiceConnectionContext(message);
            AddClientConnection(connection);
            Log.ConnectedStarting(_logger);

            // Execute the application code
            connection.ApplicationTask = _connectionDelegate(connection);

            // Writing from the application to the service
            _ = ProcessOutgoingMessagesAsync(connection);

            // Waiting for the application to shutdown so we can clean up the connection
            _ = WaitOnApplicationTask(connection);
            return Task.CompletedTask;
        }

        private async Task WaitOnApplicationTask(ServiceConnectionContext connection)
        {
            Exception exception = null;

            try
            {
                // Wait for the application task to complete
                await connection.ApplicationTask;
            }
            catch (Exception ex)
            {
                // Capture the exception to communicate it to the transport (this isn't strictly required)
                exception = ex;
            }
            finally
            {
                // Close the transport side since the application is no longer running
                connection.Transport.Output.Complete(exception);
                connection.Transport.Input.Complete();
            }

            // If we aren't already aborted, we send the abort message to the service
            if (connection.AbortOnClose)
            {
                await AbortClientConnection(connection);
            }
        }

        private async Task AbortClientConnection(ServiceConnectionContext connection)
        {
            // Inform the Service that we will remove the client because SignalR told us it is disconnected.
            var serviceMessage = new CloseConnectionMessage(connection.ConnectionId, "");
            await WriteAsync(serviceMessage);
            Log.InformServiceToRemoveClient(_logger, connection.ConnectionId);
        }

        private async Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            await PerformDisconnectAsync(closeConnectionMessage.ConnectionId);
        }

        private async Task PerformDisconnectAsync(string connectionId)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var connection))
            {
                // Service already knows the client is closed, no need to be informed.
                connection.AbortOnClose = false;

                // We're done writing to the application output
                connection.Application.Output.Complete();

                // Wait on the application task to complete
                try
                {
                    await connection.ApplicationTask;
                }
                catch (Exception ex)
                {
                    Log.ApplicaitonTaskFailed(_logger, ex);
                }
            }
            // Close this connection gracefully then remove it from the list,
            // this will trigger the hub shutdown logic appropriately
            RemoveClientConnection(connectionId);
            Log.ConnectedEnding(_logger);
        }

        private async Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionDataMessage.ConnectionId, out var connection))
            {
                try
                {
                    Log.ForwardRequestToApplication(_logger);
                    // Write the raw connection payload to the pipe let the upstream handle it
                    await connection.Application.Output.WriteAsync(connectionDataMessage.Payload);
                }
                catch (Exception ex)
                {
                    Log.FailToForwardRequestToApplication(_logger, ex);
                }
            }
            else
            {
                // Unexpected error
                Log.ReceivedReorderedRequest(_logger);
            }
        }

        private static class Log
        {
            // Category: ServiceConnection
            private static readonly Action<ILogger, Exception> _writeAsyncFailure =
                LoggerMessage.Define(LogLevel.Error, new EventId(1, "WriteAsyncFailure"), "Fail to send message to the service.");

            private static readonly Action<ILogger, Exception> _connectionFailure =
                LoggerMessage.Define(LogLevel.Error, new EventId(2, "ConnectionFailure"), "Fail to connect to the service.");

            private static readonly Action<ILogger, Exception> _errorProcessingRequest =
                LoggerMessage.Define(LogLevel.Error, new EventId(3, "ErrorProcessingRequest"), "Error when processing requests.");

            private static readonly Action<ILogger, Exception> _connectionDropped =
                LoggerMessage.Define(LogLevel.Error, new EventId(4, "ConnectionDropped"), "Connection with the service was dropped.");

            private static readonly Action<ILogger, Exception> _failToCleanupConnections =
                LoggerMessage.Define(LogLevel.Error, new EventId(5, "FailToCleanupConnection"), "Fail to cleanup client connections.");

            private static readonly Action<ILogger, Exception> _errorSendingMessage =
                LoggerMessage.Define(LogLevel.Error, new EventId(6, "ErrorSendingMessage"), "Error when sending message.");

            private static readonly Action<ILogger, Exception> _sendLoopStopped =
                LoggerMessage.Define(LogLevel.Error, new EventId(7, "SendLoopStopped"), "Message sending loop stops.");

            private static readonly Action<ILogger, Exception> _applicationTaskFailed =
                LoggerMessage.Define(LogLevel.Error, new EventId(8, "ApplicationTaskFailed"), "Application task failed to complete.");

            private static readonly Action<ILogger, Exception> _failToForwardRequestToApplication =
                LoggerMessage.Define(LogLevel.Error, new EventId(9, "FailToForwardRequestToApplication"), "Fail to forward request to application by writing the pipe.");

            private static readonly Action<ILogger, Exception> _receivedReorderedRequest =
                LoggerMessage.Define(LogLevel.Error, new EventId(10, "ReceivedReorderedRequest"), "Received re-ordered request.");

            private static readonly Action<ILogger, Exception> _connectedStarting =
                LoggerMessage.Define(LogLevel.Debug, new EventId(11, "ConnectedStarting"), "OnConnectedAsync started.");

            private static readonly Action<ILogger, Exception> _connectedEnding =
                LoggerMessage.Define(LogLevel.Debug, new EventId(12, "ConnectedEnding"), "OnConnectedAsync ending.");

            private static readonly Action<ILogger, string, Exception> _informServiceToRemoveClient =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(13, "InformServiceToRemoveClient"), "Inform the service to remove client connection {connectionId}.");

            private static readonly Action<ILogger, Exception> _connectionClosed =
                LoggerMessage.Define(LogLevel.Debug, new EventId(14, "ConnectionClose"), "Connection is closed.");

            private static readonly Action<ILogger, Exception> _readingCanceled =
                LoggerMessage.Define(LogLevel.Debug, new EventId(15, "ReadingCanceled"), "Reading from transport pipe is canceled.");

            private static readonly Action<ILogger, Exception> _receivedRequest =
                LoggerMessage.Define(LogLevel.Debug, new EventId(16, "ReceivedRequest"), "Received request from service.");

            private static readonly Action<ILogger, double, Exception> _startingServerTimeoutTimer =
                LoggerMessage.Define<double>(LogLevel.Debug, new EventId(17, "StartingServerTimeoutTimer"), "Starting server timeout timer. Duration: {ServerTimeout:0.00}ms");

            private static readonly Action<ILogger, double, Exception> _serverTimeout =
                LoggerMessage.Define<double>(LogLevel.Error, new EventId(18, "ServerTimeout"), "Server timeout ({ServerTimeout:0.00}ms) elapsed without receiving a message from the server.");

            private static readonly Action<ILogger, Exception> _resettingKeepAliveTimer =
                LoggerMessage.Define(LogLevel.Trace, new EventId(19, "ResettingKeepAliveTimer"), "Resetting keep-alive timer, received a message from the server.");

            private static readonly Action<ILogger, Exception> _forwardRequestToApplication =
                LoggerMessage.Define(LogLevel.Trace, new EventId(20, "ForwardRequestToApplication"), "Forward request to application.");

            public static void FailToWriteAsync(ILogger logger, Exception exception)
            {
                _writeAsyncFailure(logger, exception);
            }

            public static void FailToConnect(ILogger logger, Exception exception)
            {
                _connectionFailure(logger, exception);
            }

            public static void ErrorProcessingRequest(ILogger logger, Exception exception)
            {
                _errorProcessingRequest(logger, exception);
            }

            public static void ConnectionDropped(ILogger logger, Exception exception)
            {
                _connectionDropped(logger, exception);
            }

            public static void FailToCleanupConnections(ILogger logger, Exception exception)
            {
                _failToCleanupConnections(logger, exception);
            }

            public static void ErrorSendingMessage(ILogger logger, Exception exception)
            {
                _errorSendingMessage(logger, exception);
            }

            public static void SendLoopStopped(ILogger logger, Exception exception)
            {
                _sendLoopStopped(logger, exception);
            }

            public static void ApplicaitonTaskFailed(ILogger logger, Exception exception)
            {
                _applicationTaskFailed(logger, exception);
            }

            public static void FailToForwardRequestToApplication(ILogger logger, Exception exception)
            {
                _failToForwardRequestToApplication(logger, exception);
            }

            public static void ReceivedReorderedRequest(ILogger logger)
            {
                _receivedReorderedRequest(logger, null);
            }

            public static void ConnectedStarting(ILogger logger)
            {
                _connectedStarting(logger, null);
            }

            public static void ConnectedEnding(ILogger logger)
            {
                _connectedEnding(logger, null);
            }

            public static void InformServiceToRemoveClient(ILogger logger, string connectionId)
            {
                _informServiceToRemoveClient(logger, connectionId, null);
            }

            public static void ConnectionClosed(ILogger logger)
            {
                _connectionClosed(logger, null);
            }

            public static void ReadingCanceled(ILogger logger)
            {
                _readingCanceled(logger, null);
            }

            public static void ReceivedRequest(ILogger logger)
            {
                _receivedRequest(logger, null);
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

            public static void ForwardRequestToApplication(ILogger logger)
            {
                _forwardRequestToApplication(logger, null);
            }
        }
    }
}
