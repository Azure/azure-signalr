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
        private readonly TimeSpan DefaultServerTimeout = TimeSpan.FromSeconds(30);// Server ping rate is 15 sec, this is 2 times that.

        private ConnectionContext _connection;
        private ConnectionDelegate _connectionDelegate;
        private ConcurrentDictionary<string, string> _connectionIds = new ConcurrentDictionary<string, string>();
        private int _reconnectIntervalInMS => StaticRandom.Next(1000);// Start reconnect after a random interval less than 1 second
        private volatile bool _connected;

        public ServiceConnection(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            IConnectionFactory connectionFactory, ILoggerFactory loggerFactory)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _logger = loggerFactory.CreateLogger<ServiceConnection>();
        }

        public async Task StartAsync(ConnectionDelegate connectionDelegate)
        {
            _connectionDelegate = connectionDelegate;
            while (true)
            {
                await StartAsyncCore();

                await ProcessIncomingAsync();
            }
        }

        public async Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (!_connected)
            {
                _logger.LogError("Connection has not been established, any data cannot be sent to service");
                return;
            }

            // We have to lock around outgoing sends since the pipe is single writer.
            // The lock is per serviceConnection
            await _serviceConnectionLock.WaitAsync();

            try
            {
                // Write the service protocol message
                _serviceProtocol.WriteMessage(serviceMessage, _connection.Transport.Output);
                await _connection.Transport.Output.FlushAsync(CancellationToken.None);
                _logger.LogDebug("Send messge to service");
            }
            catch (Exception e)
            {
                _logger.LogError($"Fail to send message through SDK <-> Service channel: {e.Message}");
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
                    _connected = true;
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to connect to Azure SignalR due to error: {e.Message}");
                    await Task.Delay(_reconnectIntervalInMS);
                }
                finally
                {
                    _serviceConnectionLock.Release();
                }
            }
        }

        private Timer StartTimeoutTimer()
        {
            _logger.LogDebug("Start server timeout timer");
            return new Timer(state => ((ServiceConnection)state).TimeoutElapsed(),
                    this, Timeout.Infinite, Timeout.Infinite);
        }

        private void ResetTimeoutTimer(Timer timeoutTimer)
        {
            if (timeoutTimer != null)
            {
                _logger.LogDebug("Reset server timeout timer");
                timeoutTimer.Change(DefaultServerTimeout, Timeout.InfiniteTimeSpan);
            }
        }

        private void TimeoutElapsed()
        {
            // Stop the reading from connection
            _connection.Transport.Input.CancelPendingRead();
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
                            _logger.LogDebug("Cancel the read from connection");
                            break;
                        }
                        if (!buffer.IsEmpty)
                        {
                            ResetTimeoutTimer(timeoutTimer);
                            _logger.LogDebug("message received from service");
                            while (_serviceProtocol.TryParseMessage(ref buffer, out ServiceMessage message))
                            {
                                _ = DispatchMessage(message);
                            }
                        }
                        else if (result.IsCompleted)
                        {
                            // The connection is closed (reconnect)
                            _logger.LogDebug("Connection is closed");
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        // Error occurs in handling the message, but the connection between SDK and service still works.
                        // So, just log error instead of breaking the connection
                        _logger.LogError($"Fail to handle message from service {e.Message}");
                    }
                    finally
                    {
                        _connection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            catch (Exception e)
            {
                // Fatal error: There is something wrong for the connection between SDK and service.
                // Abort all the client connections, close the httpConnection.
                // Only reconnect can recover.
                _logger.LogError($"connection between SDK and service drops for {e.Message}");
            }
            finally
            {
                await _connectionFactory.DisposeAsync(_connection);
                timeoutTimer?.Dispose();
            }
            _connected = false;
            // TODO: Never cleanup connections unless Service asks us to do that
            // Current implementation is based on assumption that Service will drop clients
            // if server connection fails.
            await CleanupConnections();
        }

        private async Task CleanupConnections()
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

                        _logger.LogDebug($"Send data message back to client through {connection.ConnectionId}");
                    }
                    else if (result.IsCompleted)
                    {
                        // This connection ended (the application itself shut down) we should remove it from the list of connections
                        break;
                    }
                    connection.Application.Input.AdvanceTo(buffer.End);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Error occurs when reading from SignalR or sending to Service: {e.Message}");
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
            _logger.LogDebug($"Remove client connection {connectionId}");
            _connectionIds.TryRemove(connectionId, out _);
        }

        private Task OnConnectedAsync(OpenConnectionMessage message)
        {
            var connection = new ServiceConnectionContext(message);
            AddClientConnection(connection);
            _logger.LogDebug("Handle OnConnected command");

            // Execute the application code, this will call into the SignalR end point
            // SignalR keeps on reading from Transport.Input.
            connection.ApplicationTask = _connectionDelegate(connection);
            // Sending SignalR output
            _ = ProcessOutgoingMessagesAsync(connection);
            _ = WaitOnAppTasks(connection.ApplicationTask, connection);
            return Task.CompletedTask;
        }

        private async Task WaitOnAppTasks(Task applicationTask, ServiceConnectionContext connection)
        {
            await applicationTask;
            // SignalR stops reading
            connection.Transport.Output.Complete(applicationTask.Exception?.InnerException);
            connection.Transport.Input.Complete();
            // If Service has already dropped the client, no need to send Abort message.
            if (!connection.AbortOnClose)
            {
                await AbortClientConnection(connection);
            }
        }

        private async Task WaitOnTransportTask(ServiceConnectionContext connection)
        {
            connection.Application.Output.Complete();
            try
            {
                await connection.ApplicationTask;
            }
            finally
            {
                connection.Application.Input.Complete();
            }
        }

        private async Task AbortClientConnection(ServiceConnectionContext connection)
        {
            // Inform the Service that we will remove the client because SignalR told us it is disconnected.
            var serviceMessage = new CloseConnectionMessage(connection.ConnectionId, "");
            await WriteAsync(serviceMessage);
            _logger.LogDebug($"Inform service that client {connection.ConnectionId} should be removed");
        }

        private async Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            await PerformDisconnectAsync(closeConnectionMessage.ConnectionId);
        }

        private async Task PerformDisconnectAsync(string connectionId)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var connection))
            {
                connection.AbortOnClose = true;
                await WaitOnTransportTask(connection);
            }
            // Close this connection gracefully then remove it from the list,
            // this will trigger the hub shutdown logic appropriately
            RemoveClientConnection(connectionId);
        }

        private async Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionDataMessage.ConnectionId, out var connection))
            {
                try
                {
                    _logger.LogDebug("Send message to SignalR Hub handler");
                    // Write the raw connection payload to the pipe let the upstream handle it
                    await connection.Application.Output.WriteAsync(connectionDataMessage.Payload);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Fail to write message to application pipe: {e.Message}");
                }
            }
            else
            {
                _logger.LogError("Message re-ordered");
                // Unexpected error
            }
        }
    }
}
