// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    public class ServiceConnection
    {
        public static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);

        private HttpConnection _httpConnection;
        private IClientConnectionManager _clientConnectionManager;
        private ConnectionDelegate _connectionDelegate;
        private SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);
        private readonly ILogger<ServiceConnection> _logger;

        public TimeSpan HandshakeTimeout { get; set; } = DefaultHandshakeTimeout;

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
            //await HandshakeAsync();
            //_logger.LogDebug("Finish handshake with service");
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
            }
            finally
            {
                _serviceConnectionLock.Release();
            }
        }

        private async Task HandshakeAsync()
        {
            var handshakeRequest = new HandshakeRequestMessage(ServiceProtocol.Name, ServiceProtocol.Version);
            HandshakeProtocol.WriteRequestMessage(handshakeRequest, _httpConnection.Transport.Output);
            var sendHandshakeResult = await _httpConnection.Transport.Output.FlushAsync(CancellationToken.None);

            if (sendHandshakeResult.IsCompleted)
            {
                // The other side disconnected
                throw new InvalidOperationException("The server disconnected before the handshake was completed");
            }
            await ProcessHandshakeResponseAsync(_httpConnection.Transport);
        }

        private async Task ProcessIncomingAsync()
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
                        if (ServiceProtocol.TryParseMessage(ref buffer, out ServiceMessage message))
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
                catch(Exception e)
                {
                    _logger.LogError($"Fail to handle message from service {e.Message}");
                    throw e;
                }
                finally
                {
                    _httpConnection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                }
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
            await ProcessHandshakeResponseAsync(connection.Application);

            try
            {
                while (true)
                {
                    var result = await connection.Application.Input.ReadAsync();
                    var buffer = result.Buffer;

                    if (!buffer.IsEmpty)
                    {
                        // Send Handshake, Completion or Error back to the Client
                        var serviceMessage = new ServiceMessage();
                        if (!connection.FinishedHandshake)
                        {
                            connection.FinishedHandshake = true;
                            serviceMessage.CreateSendConnection(connection.ConnectionId, "json", buffer.ToArray());
                        }
                        else
                        {
                            serviceMessage.CreateSendConnection(connection.ConnectionId, connection.ProtocolName, buffer.ToArray())
                                      .AddProtocolName(connection.ProtocolName); // for debug
                        }
                        await SendServiceMessage(serviceMessage);
                    }
                    else if (result.IsCompleted)
                    {
                        // This connection ended (the application itself shut down) we should remove it from the list of connections
                        break;
                    }
                    connection.Application.Input.AdvanceTo(buffer.Start, buffer.End);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Error occurs when reading from SignalR or sending to Service: {e.Message}");
                throw e;
            }
            finally
            {
                connection.Application.Input.Complete();
            }

            // We should probably notify the service here that the application chose to abort the connection
            _clientConnectionManager.ClientConnections.TryRemove(connection.ConnectionId, out _);
        }

        private async Task ProcessHandshakeResponseAsync(IDuplexPipe application)
        {
            try
            {
                using (var handshakeCts = new CancellationTokenSource(HandshakeTimeout))
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(default, handshakeCts.Token))
                {
                    while (true)
                    {
                        var result = await application.Input.ReadAsync();
                        var buffer = result.Buffer;
                        var consumed = buffer.Start;
                        var examined = buffer.End;

                        try
                        {
                            // Read first message out of the incoming data
                            if (!buffer.IsEmpty)
                            {
                                if (HandshakeProtocol.TryParseResponseMessage(ref buffer, out var message))
                                {
                                    // Adjust consumed and examined to point to the end of the handshake
                                    // response, this handles the case where invocations are sent in the same payload
                                    // as the the negotiate response.
                                    consumed = buffer.Start;
                                    examined = consumed;

                                    if (message.Error != null)
                                    {
                                        var error = $"Unable to complete handshake with the server due to an error: {message.Error}";
                                        _logger.LogError(error);
                                        //Log.HandshakeServerError(_logger, message.Error);
                                        throw new Exception(error);
                                    }
                                    break;
                                }
                            }
                            else if (result.IsCompleted)
                            {
                                // Not enough data, and we won't be getting any more data.
                                throw new InvalidOperationException(
                                    "The server disconnected before sending a handshake response");
                            }
                        }
                        finally
                        {
                            application.Input.AdvanceTo(consumed, examined);
                        }
                    }
                }
            }
            // Ignore HubException because we throw it when we receive a handshake response with an error
            // And we don't need to log that the handshake failed
            catch (Exception ex)
            {
                // shutdown if we're unable to read handshake
                //Log.ErrorReceivingHandshakeResponse(_logger, ex);
                _logger.LogError($"Hand shake failed because we received response {ex.Message}");
                throw ex;
            }
        }

        private async Task OnConnectedAsync(ServiceMessage message)
        {
            var connection = new ServiceConnectionContext(message);
            _clientConnectionManager.AddClientConnection(connection);
            _logger.LogDebug("Handle OnConnected command");
            // This is a bit hacky, we can look at how to work around this
            // We need to do fake in memory handshake between this code and the 
            // HubConnectionHandler to set the protocol
            await connection.Application.Output.WriteAsync(message.Payloads["json"]); // forward handshake
            // Execute the application code, this will call into the SignalR end point
            _ = _connectionDelegate(connection);
            // Start receiving
            _ = ProcessOutgoingMessagesAsync(connection);
        }

        private Task OnDisconnectedAsync(ServiceMessage message)
        {
            _clientConnectionManager.ClientConnections.TryRemove(message.GetConnectionId(), out var connection);
            // Close this connection gracefully then remove it from the list, this will trigger the hub shutdown logic appropriately
            connection.Application.Output.Complete();
            return Task.CompletedTask;
        }

        private async Task OnMessageAsync(ServiceMessage message)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(message.GetConnectionId(), out var connection))
            {
                try
                {
                    _logger.LogDebug("Send message to SignalR Hub handler");
                    // Write the raw connection payload to the pipe let the upstream handle it
                    await connection.Application.Output.WriteAsync(message.Payloads[connection.ProtocolName]);
                }
                catch (Exception e)
                {
                    _logger.LogError($"SignalR reports error: {e.Message}");
                    throw e;
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
