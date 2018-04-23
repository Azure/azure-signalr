// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnection
    {
        public static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);

        private HttpConnection _httpConnection;
        private IServiceProtocol _serviceProtocol;
        private IClientConnectionManager _clientConnectionManager;
        private ConnectionDelegate _connectionDelegate;
        private SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);
        private readonly ILogger<ServiceConnection> _logger;

        public static TimeSpan HandshakeTimeout { get; set; } = DefaultHandshakeTimeout;

        public ServiceConnection(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            Uri serviceUrl, HttpConnection httpConnection, ILoggerFactory loggerFactory)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _httpConnection = httpConnection;
            _logger = loggerFactory.CreateLogger<ServiceConnection>();
        }

        public async Task StartAsync(ConnectionDelegate connectionDelegate)
        {
            _connectionDelegate = connectionDelegate;
            await _httpConnection.StartAsync(TransferFormat.Binary);
            // TODO. Handshake with Service for version confirmation
            _ = ProcessIncomingAsync();
        }

        public async Task SendServiceMessage(ServiceMessage serviceMessage)
        {
            try
            {
                // We have to lock around outgoing sends since the pipe is single writer.
                // The lock is per serviceConnection
                await _serviceConnectionLock.WaitAsync();

                // Write the service protocol message
                _serviceProtocol.WriteMessage(serviceMessage, _httpConnection.Transport.Output);
                await _httpConnection.Transport.Output.FlushAsync(CancellationToken.None);
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

        private async Task ProcessIncomingAsync()
        {
            try
            {
                while (true)
                {
                    var result = await _httpConnection.Transport.Input.ReadAsync();
                    var buffer = result.Buffer;

                    try
                    {
                        if (!buffer.IsEmpty)
                        {
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
                        _httpConnection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            catch (Exception)
            {
                // Fatal error: There is something wrong for the connection between SDK and service.
                // Abort all the client connections, close the httpConnection.
                // Only reconnect can recover.
            }
            finally
            {
                await _httpConnection.DisposeAsync();
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
                        // Forward the message to the service
                        if (buffer.IsSingleSegment)
                        {
                            await SendServiceMessage(new ConnectionDataMessage(connection.ConnectionId, buffer.First));
                        }
                        else
                        {
                            // This is a multi-segmented buffer so just write each chunk
                            // TODO: Optimize this by doing it all under a single lock
                            var position = buffer.Start;
                            while (buffer.TryGet(ref position, out var memory))
                            {
                                await SendServiceMessage(new ConnectionDataMessage(connection.ConnectionId, memory));
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

        private Task OnConnectedAsync(OpenConnectionMessage message)
        {
            var connection = new ServiceConnectionContext(message);
            _clientConnectionManager.AddClientConnection(connection);
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
            await AbortClientConnection(connection);
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
            await SendServiceMessage(serviceMessage);
            _logger.LogDebug($"Inform service that client {connection.ConnectionId} should be removed");
        }

        private async Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(closeConnectionMessage.ConnectionId, out var connection))
            {
                await WaitOnTransportTask(connection);
            }
            // Close this connection gracefully then remove it from the list, this will trigger the hub shutdown logic appropriately
            _clientConnectionManager.ClientConnections.TryRemove(closeConnectionMessage.ConnectionId, out _);
            _logger.LogDebug($"Remove client connection {connection.ConnectionId}");
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
