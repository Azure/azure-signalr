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

namespace Microsoft.Azure.SignalR
{
    public class ServiceConnection
    {
        // Only Binary TransferFormat is supported for SDK and service
        private readonly TransferFormat _transferFormat = TransferFormat.Binary;
        private HttpConnection _httpConnection;
        private IClientConnectionManager _clientConnectionManager;
        private ConnectionDelegate _connectionDelegate;
        private SemaphoreSlim _serviceConnectionLock = new SemaphoreSlim(1, 1);

        public ServiceConnection(IClientConnectionManager clientConnectionManager,
            Uri serviceUrl, HttpConnection httpConnection)
        {
            _clientConnectionManager = clientConnectionManager;
            _httpConnection = httpConnection;
        }
        
        public async Task StartAsync(ConnectionDelegate connectionDelegate)
        {
            _connectionDelegate = connectionDelegate;
            await _httpConnection.StartAsync(_transferFormat);
            await HandshakeAsync();
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
                var bytes = ServiceProtocol.WriteToArray(serviceMessage);

                _httpConnection.Transport.Output.Write(bytes);
                await _httpConnection.Transport.Output.FlushAsync();
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

                if (!buffer.IsEmpty)
                {
                    while (ServiceProtocol.TryParseMessage(ref buffer, out ServiceMessage message))
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
                                _ = OnSignalRHubCallAsync(message);
                                break;
                        }
                    }
                }
                else if (result.IsCompleted)
                {
                    // The connection is closed (reconnect)
                    break;
                }

                _httpConnection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
            }
        }

        private async Task ProcessOutgoingAckAsync(ServiceConnectionContext connection)
        {
            await ProcessHandshakeResponseAsync(connection.Application);

            while (true)
            {
                var result = await connection.Application.Input.ReadAsync();
                var buffer = result.Buffer;

                if (!buffer.IsEmpty)
                {
                    // Send Completion or Error back to the Client
                    var serviceMessage = new ServiceMessage();
                    serviceMessage.CreateSendConnection(connection.ConnectionId, connection.ProtocolName, buffer.ToArray());
                    await SendServiceMessage(serviceMessage);
                }
                else if (result.IsCompleted)
                {
                    // This connection ended (the application itself shut down) we should remove it from the list of connections
                    break;
                }
                connection.Application.Input.AdvanceTo(buffer.Start, buffer.End);
            }

            // We should probably notify the service here that the application chose to abort the connection
            _clientConnectionManager.ClientConnections.TryRemove(connection.ConnectionId, out _);
        }

        private async Task ProcessHandshakeResponseAsync(IDuplexPipe application)
        {
            try
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
                                    //Log.HandshakeServerError(_logger, message.Error);
                                    throw new Exception(
                                        $"Unable to complete handshake with the server due to an error: {message.Error}");
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
            // Ignore HubException because we throw it when we receive a handshake response with an error
            // And we don't need to log that the handshake failed
            catch (Exception ex)
            {
                // shutdown if we're unable to read handshake
                //Log.ErrorReceivingHandshakeResponse(_logger, ex);
                throw ex;
            }
        }

        private async Task OnConnectedAsync(ServiceMessage message)
        {
            var connection = new ServiceConnectionContext(message);
            _clientConnectionManager.AddClientConnection(connection);

            // Start receiving
            _ = ProcessOutgoingAckAsync(connection);
            // This is a bit hacky, we can look at how to work around this
            // We need to do fake in memory handshake between this code and the 
            // HubConnectionHandler to set the protocol
            HandshakeRequestMessage handshakeRequest;
            handshakeRequest = new HandshakeRequestMessage(message.GetProtocolName(), message.GetProtocolVersion());
            HandshakeProtocol.WriteRequestMessage(handshakeRequest, connection.Application.Output);
            
            var sendHandshakeResult = await connection.Application.Output.FlushAsync(CancellationToken.None);

            if (sendHandshakeResult.IsCompleted)
            {
                // The other side disconnected
                throw new InvalidOperationException("The server disconnected before the handshake was completed");
            }
            // Execute the application code, this will call into the SignalR end point
            _ = _connectionDelegate(connection);
        }

        private Task OnDisconnectedAsync(ServiceMessage message)
        {
            _clientConnectionManager.ClientConnections.TryRemove(message.GetConnectionId(), out var connection);
            // Close this connection gracefully then remove it from the list, this will trigger the hub shutdown logic appropriately
            connection.Application.Output.Complete();
            return Task.CompletedTask;
        }

        private async Task OnSignalRHubCallAsync(ServiceMessage message)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(message.GetConnectionId(), out var connection))
            {
                // Write the raw connection payload to the pipe let the upstream handle it
                await connection.Application.Output.WriteAsync(message.Payloads[connection.ProtocolName]);
            }
            else
            {
                // Unexpected error
            }
        }
    }
}
