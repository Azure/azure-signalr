// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnection
    {
        public static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);

        private HttpConnection _httpConnection;
        private IClientConnectionManager _clientConnectionManager;
        private ConnectionDelegate _connectionDelegate;
        private SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);
        private readonly ILogger<ServiceConnection> _logger;

        public static TimeSpan HandshakeTimeout { get; set; } = DefaultHandshakeTimeout;

        public ServiceConnection(IClientConnectionManager clientConnectionManager,
            Uri serviceUrl, HttpConnection httpConnection, ILoggerFactory loggerFactory)
        {
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
                ServiceProtocol.WriteMessage(serviceMessage, _httpConnection.Transport.Output);
                await _httpConnection.Transport.Output.FlushAsync(CancellationToken.None);
                _logger.LogDebug("Send messge to service");
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
                            while (ServiceProtocol.TryParseMessage(ref buffer, out ServiceMessage message))
                            {
                                await DispatchMessage(message);
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
                        _logger.LogError($"Fail to handle message from service {e.Message}");
                    }
                    finally
                    {
                        _httpConnection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // There is something wrong for the connection between SDK and service.
                // Abort all the client connections, close the httpConnection.
                await _httpConnection.DisposeAsync();
            }
        }

        private async Task DispatchMessage(ServiceMessage message)
        {
            _logger.LogDebug($"mesage command {message.Command}");
            if (message.Command != CommandType.Ping)
            {
                switch (message.Command)
                {
                    case CommandType.AddConnection:
                        await OnConnectedAsync(message);
                        break;
                    case CommandType.RemoveConnection:
                        await OnDisconnectedAsync(message);
                        break;
                    default:
                        _ = OnMessageAsync(message);
                        break;
                }
            }
            // ignore ping
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
                        // Blindly send message from SignalR to Client
                        var serviceMessage = new ServiceMessage();
                        serviceMessage.CreateAckResponse(connection.ConnectionId, buffer.ToArray());
                        await SendServiceMessage(serviceMessage);
                        _logger.LogDebug($"Send Ack message {serviceMessage.Command}");
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

        private async Task OnConnectedAsync(ServiceMessage message)
        {
            var connection = new ServiceConnectionContext(message);
            _clientConnectionManager.AddClientConnection(connection);
            _logger.LogDebug("Handle OnConnected command");

            // Need to remove the workaround handshake after fixing https://github.com/Azure/azure-signalr/issues/20
            await connection.Application.Output.WriteAsync(message.Payloads["json"]);
            // Execute the application code, this will call into the SignalR end point
            // SignalR keeps on reading from Transport.Input.
            _ = _connectionDelegate(connection).ContinueWith(task =>
            {
                // SignalR stops reading from Transport.Input, we have to complete pipes for
                // both Transport and Application, and then remove the connection.

                // Here we complete the Transport.Output, which triggers
                // connection.Application.Input.ReadAsync returns "Completed",
                // and connection.Applicaiton.Input.Complete()
                connection.Transport.Output.Complete();
                _logger.LogDebug($"SignalR completes reading from Transport.Input which finally causes {connection.ConnectionId} to be removed");
            });
            // Start receiving
            connection.ApplicationTask = ProcessOutgoingMessagesAsync(connection);
        }

        private async Task RemoveConnectionContext(ServiceConnectionContext connection)
        {
            // Inform the Service that we will remove the client because SignalR told us it is disconnected.
            var serviceMessage = new ServiceMessage();
            serviceMessage.CreateAbortConnection(connection.ConnectionId);
            await SendServiceMessage(serviceMessage);
            _logger.LogDebug($"Inform service that client {connection.ConnectionId} should be removed");
            // Remove the connection
            _clientConnectionManager.ClientConnections.TryRemove(connection.ConnectionId, out _);
            _logger.LogDebug($"{connection.ConnectionId} is removed");
        }

        private async Task OnDisconnectedAsync(ServiceMessage message)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(message.GetConnectionId(), out var connection))
            {
                connection.Application.Output.Complete();
                // wait for application.Input.ReadAsync() to complete
                await connection.ApplicationTask;
            }
            // Close this connection gracefully then remove it from the list, this will trigger the hub shutdown logic appropriately
            _clientConnectionManager.ClientConnections.TryRemove(message.GetConnectionId(), out _);
        }

        private async Task OnMessageAsync(ServiceMessage message)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(message.GetConnectionId(), out var connection))
            {
                try
                {
                    _logger.LogDebug("Send message to SignalR Hub handler");
                    // Write the raw connection payload to the pipe let the upstream handle it
                    // Remove the dependence on ProtocolName after fixing https://github.com/Azure/azure-signalr/issues/20
                    await connection.Application.Output.WriteAsync(message.Payloads[connection.ProtocolName]);
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
