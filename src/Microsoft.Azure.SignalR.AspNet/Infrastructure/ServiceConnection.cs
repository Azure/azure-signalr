// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal partial class ServiceConnection : ServiceConnectionBase
    {
        private const string ReconnectMessage = "asrs:reconnect";
        private readonly ConcurrentDictionary<string, IServiceTransport> _clientConnections = new ConcurrentDictionary<string, IServiceTransport>(StringComparer.Ordinal);

        private readonly string _hubName;
        private readonly IConnectionFactory _connectionFactory;
        private readonly IClientConnectionManager _clientConnectionManager;

        public ServiceConnection(
            string hubName,
            string connectionId,
            IServiceProtocol serviceProtocol,
            IConnectionFactory connectionFactory,
            IClientConnectionManager clientConnectionManager,
            ILogger logger)
            : base(serviceProtocol, logger, connectionId)
        {
            _hubName = hubName;
            _connectionFactory = connectionFactory;
            _clientConnectionManager = clientConnectionManager;
        }

        protected override Task<ConnectionContext> CreateConnection()
        {
            return _connectionFactory.ConnectAsync(TransferFormat.Binary, _connectionId, _hubName);
        }

        protected override Task DisposeConnection()
        {
            var connection = _connection;
            _connection = null;
            return _connectionFactory.DisposeAsync(connection);
        }

        protected override async Task CleanupConnections()
        {
            try
            {
                foreach (var connection in _clientConnections)
                {
                    await PerformDisconnectCore(connection.Key);
                }
            }
            catch (Exception ex)
            {
                Log.FailedToCleanupConnections(_logger, ex);
            }
        }

        protected override async Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            // Create channel per client to async process message
            var connectionId = openConnectionMessage.ConnectionId;
            var channel = Channel.CreateUnbounded<ServiceMessage>();
            try
            {
                await channel.Writer.WriteAsync(openConnectionMessage);
                var transport = new AzureTransport(connectionId, channel);
                _clientConnections.TryAdd(connectionId, transport);

                // Writing from the application to the service
                _ = ProcessMessageAsync(connectionId);
            }
            catch (Exception e)
            {
                // Fail to write initial open connection message to channel
                Log.ConnectedStartingFailed(_logger, connectionId, e);
                // Close channel and notify client to close connection
                channel.Writer.TryComplete(e);
                await WriteAsync(new CloseConnectionMessage(connectionId, e.Message));
            }
        }

        protected override async Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            var connectionId = closeConnectionMessage.ConnectionId;
            if (_clientConnections.TryGetValue(connectionId, out var transport))
            {
                try
                {
                    await transport.Channel.Writer.WriteAsync(closeConnectionMessage);
                }
                catch (Exception e)
                {
                    Log.FailToWriteMessageToApplication(_logger, connectionId, e);
                }
            }
        }

        protected override async Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            var connectionId = connectionDataMessage.ConnectionId;
            if (_clientConnections.TryGetValue(connectionId, out var transport))
            {
                try
                {
                    await transport.Channel.Writer.WriteAsync(connectionDataMessage);
                }
                catch (Exception e)
                {
                    Log.FailToWriteMessageToApplication(_logger, connectionId, e);
                }
            }
        }

        private async Task PerformDisconnectCore(string connectionId)
        {
            Exception exception = null;
            if (_clientConnections.TryRemove(connectionId, out var transport))
            {
                try
                {
                    // Mark channel complete and wait for reader
                    transport.Channel.Writer.TryComplete();
                    await transport.Channel.Reader.Completion;
                }
                catch (Exception e)
                {
                    exception = e;
                }
                finally
                {
                    transport.OnDisconnected();
                }
            }

            Log.ConnectedEnding(_logger, connectionId);
        }

        private async Task OnConnectedAsyncCore(OpenConnectionMessage message)
        {
            var connectionId = message.ConnectionId;
            if (_clientConnections.TryGetValue(connectionId, out var transport))
            {
                try
                {
                    var clientTransport = _clientConnectionManager.CreateConnection(message, this);
                    // Transfer channel and update client connection dictionary with real transport
                    clientTransport.Channel = transport.Channel;
                    _clientConnections.TryUpdate(connectionId, clientTransport, transport);
                    Log.ConnectedStarting(_logger, connectionId);
                }
                catch (Exception e)
                {
                    Log.ConnectedStartingFailed(_logger, connectionId, e);
                    await PerformDisconnectCore(connectionId);
                    await WriteAsync(new CloseConnectionMessage(connectionId, e.Message));
                }
            }
        }

        private void ProcessOutgoingMessages(ConnectionDataMessage connectionDataMessage)
        {
            var connectionId = connectionDataMessage.ConnectionId;
            if (_clientConnections.TryGetValue(connectionId, out var transport))
            {
                try
                {
                    var payload = connectionDataMessage.Payload;
                    Log.WriteMessageToApplication(_logger, payload.Length, connectionId);
                    var message = GetString(payload);
                    if (message == ReconnectMessage)
                    {
                        transport.Reconnected?.Invoke();
                    }
                    else
                    {
                        transport.OnReceived(message);
                    }
                }
                catch (Exception e)
                {
                    Log.FailToWriteMessageToApplication(_logger, connectionDataMessage.ConnectionId, e);
                }
            }
            else
            {
                // Unexpected error
                Log.ReceivedMessageForNonExistentConnection(_logger, connectionDataMessage.ConnectionId);
            }
        }

        private async Task ProcessMessageAsync(string connectionId)
        {
            // Check if channel is created.
            if (_clientConnections.TryGetValue(connectionId, out var transport))
            {
                try
                {
                    // Check if channel is closed.
                    while (await transport.Channel.Reader.WaitToReadAsync())
                    {
                        while (transport.Channel.Reader.TryRead(out var serviceMessage))
                        {
                            switch (serviceMessage)
                            {
                                case OpenConnectionMessage openConnectionMessage:
                                    await OnConnectedAsyncCore(openConnectionMessage);
                                    break;
                                case CloseConnectionMessage closeConnectionMessage:
                                    await PerformDisconnectCore(closeConnectionMessage.ConnectionId);
                                    break;
                                case ConnectionDataMessage connectionDataMessage:
                                    ProcessOutgoingMessages(connectionDataMessage);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // Internal exception is already catched and here only for channel exception.
                    // Notify client to disconnect.
                    Log.SendLoopStopped(_logger, connectionId, e);
                    await PerformDisconnectCore(connectionId);
                    await WriteAsync(new CloseConnectionMessage(connectionId, e.Message));
                }
            }
        }

        private static string GetString(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                MemoryMarshal.TryGetArray(buffer.First, out var segment);
                return Encoding.UTF8.GetString(segment.Array, segment.Offset, segment.Count);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
    }
}
