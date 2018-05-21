// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    internal partial class ServiceConnection
    {
        private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);
        // Service ping rate is 15 sec; this is 2 times that.
        private static readonly TimeSpan DefaultServiceTimeout = TimeSpan.FromSeconds(30);
        private static readonly long DefaultServiceTimeoutTicks = DefaultServiceTimeout.Seconds * Stopwatch.Frequency;
        // App server ping rate is 5 sec. So service can detect an irresponsive server connection in 10 seconds at most.
        private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(5);
        private static readonly int MaxReconnectBackoffInternalInMilliseconds = 1000;

        private readonly IConnectionFactory _connectionFactory;
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);
        private readonly ILogger<ServiceConnection> _logger;
        private readonly HandshakeRequestMessage _handshakeRequest;
        private readonly ReadOnlyMemory<byte> _cachedPingBytes;

        private readonly ConcurrentDictionary<string, string> _connectionIds =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private readonly string _connectionId;
        private readonly ConnectionDelegate _connectionDelegate;
        private ConnectionContext _connection;
        private bool _isStopped;
        private long _lastReceiveTimestamp;

        // Start reconnect after a random interval less than 1 second
        private static TimeSpan ReconnectInterval =>
            TimeSpan.FromMilliseconds(StaticRandom.Next(MaxReconnectBackoffInternalInMilliseconds));

        public ServiceConnection(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory, ILoggerFactory loggerFactory,
            ConnectionDelegate connectionDelegate, string connectionId)
        {
            _serviceProtocol = serviceProtocol;
            _handshakeRequest = new HandshakeRequestMessage(_serviceProtocol.Version);
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _connectionDelegate = connectionDelegate;
            _connectionId = connectionId;
            _logger = loggerFactory.CreateLogger<ServiceConnection>();

            _cachedPingBytes = _serviceProtocol.GetMessageBytes(PingMessage.Instance);
        }

        public async Task StartAsync()
        {
            while (!_isStopped)
            {
                // If we are not able to start, we will quit this connection.
                if (!await StartAsyncCore())
                {
                    return;
                }

                await ProcessIncomingAsync();
            }
        }

        // For test purpose only
        internal Task StopAsync()
        {
            _isStopped = true;
            _connection?.Transport.Input.CancelPendingRead();
            return Task.CompletedTask;
        }

        public async Task WriteAsync(ServiceMessage serviceMessage)
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

        private async Task<bool> StartAsyncCore()
        {
            // Always try until connected
            while (true)
            {
                // Lock here in case somebody tries to send before the connection is assigned
                await _serviceConnectionLock.WaitAsync();

                try
                {
                    _connection = await _connectionFactory.ConnectAsync(TransferFormat.Binary, _connectionId);

                    if (await HandshakeAsync())
                    {
                        Log.ServiceConnectionConnected(_logger, _connectionId);
                        return true;
                    }
                    else
                    {
                        // False means we got a HandshakeResponseMessage with error. Will take below actions:
                        // - Dispose the connection
                        // - Stop reconnect
                        await _connectionFactory.DisposeAsync(_connection);
                        _connection = null;

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.FailedToConnect(_logger, ex);

                    if (_connection != null)
                    {
                        await _connectionFactory.DisposeAsync(_connection);
                        _connection = null;
                    }

                    await Task.Delay(ReconnectInterval);
                }
                finally
                {
                    _serviceConnectionLock.Release();
                }
            }
        }

        private async Task<bool> HandshakeAsync()
        {
            await SendHandshakeRequestAsync(_connection.Transport.Output);

            try
            {
                using (var cts = new CancellationTokenSource(DefaultHandshakeTimeout))
                {
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

        private Timer StartKeepAliveTimer()
        {
            Log.StartingKeepAliveTimer(_logger, DefaultKeepAliveInterval);

            _lastReceiveTimestamp = Stopwatch.GetTimestamp();

            var timer = new Timer(TimeoutElapsed);
            timer.Change(DefaultKeepAliveInterval, Timeout.InfiniteTimeSpan);
            return timer;
        }

        private void UpdateReceiveTimestamp()
        {
            Interlocked.Exchange(ref _lastReceiveTimestamp, Stopwatch.GetTimestamp());
        }

        private void TimeoutElapsed(object state)
        {
            if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastReceiveTimestamp) > DefaultServiceTimeoutTicks)
            {
                AbortConnection();
                return;
            }

            // Send PingMessage to Service
            _ = TrySendPingAsync();

            Log.ResettingKeepAliveTimer(_logger);
            var timer = (Timer) state;
            timer.Change(DefaultKeepAliveInterval, Timeout.InfiniteTimeSpan);
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
                keepAliveTimer.Dispose();
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
                Log.FailedToCleanupConnections(_logger, ex);
            }
        }

        private Task DispatchMessageAsync(ServiceMessage message)
        {
            switch (message)
            {
                case OpenConnectionMessage openConnectionMessage:
                    return OnConnectedAsync(openConnectionMessage);
                case CloseConnectionMessage closeConnectionMessage:
                    return PerformDisconnectAsync(closeConnectionMessage.ConnectionId);
                case ConnectionDataMessage connectionDataMessage:
                    return OnMessageAsync(connectionDataMessage);
                case PingMessage _:
                    // ignore ping
                    break;
            }
            return Task.CompletedTask;
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

                    if (result.IsCompleted)
                    {
                        // This connection ended (the application itself shut down) we should remove it from the list of connections
                        break;
                    }

                    connection.Application.Input.AdvanceTo(buffer.End);
                }
            }
            catch (Exception ex)
            {
                Log.SendLoopStopped(_logger, connection.ConnectionId, ex);
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
            _clientConnectionManager.RemoveClientConnection(connectionId);
            _connectionIds.TryRemove(connectionId, out _);
        }

        private Task OnConnectedAsync(OpenConnectionMessage message)
        {
            var connection = new ServiceConnectionContext(message);
            AddClientConnection(connection);
            Log.ConnectedStarting(_logger, connection.ConnectionId);

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
                // Inform the Service that we will remove the client because SignalR told us it is disconnected.
                var serviceMessage = new CloseConnectionMessage(connection.ConnectionId, errorMessage: "");
                await WriteAsync(serviceMessage);
                Log.CloseConnection(_logger, connection.ConnectionId);
            }
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
            Log.ConnectedEnding(_logger, connectionId);
        }

        private async Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionDataMessage.ConnectionId, out var connection))
            {
                try
                {
                    Log.WriteMessageToApplication(_logger, connectionDataMessage.Payload.Length, connectionDataMessage.ConnectionId);
                    // Write the raw connection payload to the pipe let the upstream handle it
                    await connection.Application.Output.WriteAsync(connectionDataMessage.Payload);
                }
                catch (Exception ex)
                {
                    Log.FailToWriteMessageToApplication(_logger, connectionDataMessage.ConnectionId, ex);
                }
            }
            else
            {
                // Unexpected error
                Log.ReceivedMessageForNonExistentConnection(_logger, connectionDataMessage.ConnectionId);
            }
        }
    }
}
