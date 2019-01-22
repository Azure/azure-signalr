﻿// Copyright (c) Microsoft. All rights reserved.
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
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal partial class ServiceConnection : ServiceConnectionBase
    {
        private const string ReconnectMessage = "asrs:reconnect";
        private readonly ConcurrentDictionary<string, ClientContext> _clientConnections = new ConcurrentDictionary<string, ClientContext>(StringComparer.Ordinal);

        private readonly IConnectionFactory _connectionFactory;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly ILogger _logger;

        public ServiceConnection(
            string connectionId,
            IServiceProtocol serviceProtocol,
            IConnectionFactory connectionFactory,
            IClientConnectionManager clientConnectionManager,
            ILoggerFactory loggerFactory,
            SignalR.IServiceConnectionManager manager,
            ServerConnectionType connectionType = ServerConnectionType.Default)
            : base(serviceProtocol, loggerFactory, connectionId, manager, connectionType)
        {
            _connectionFactory = connectionFactory;
            _clientConnectionManager = clientConnectionManager;
            _logger = loggerFactory?.CreateLogger<ServiceConnection>() ?? NullLogger<ServiceConnection>.Instance;
        }

        protected override Task<ConnectionContext> CreateConnection(string target = null)
        {
            return _connectionFactory.ConnectAsync(TransferFormat.Binary, ConnectionId, target);
        }

        protected override Task DisposeConnection()
        {
            var connection = ConnectionContext;
            ConnectionContext = null;
            return _connectionFactory.DisposeAsync(connection);
        }

        protected override Task CleanupConnections()
        {
            try
            {
                foreach(var connection in _clientConnections)
                {
                    PerformDisconnectCore(connection.Key);
                }
            }
            catch (Exception ex)
            {
                Log.FailedToCleanupConnections(_logger, ex);
            }
            return Task.CompletedTask;
        }

        protected override async Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            // Create empty transport with only channel for async processing messages
            var connectionId = openConnectionMessage.ConnectionId;
            var clientContext = new ClientContext();
            try
            {
                await clientContext.Output.WriteAsync(openConnectionMessage);
                if(!_clientConnections.TryAdd(connectionId, clientContext))
                {
                    Log.DuplicateConnectionId(_logger, connectionId, null);
                    throw new ArgumentException("ConnectionId already exists.");
                }

                // Writing from the application to the service
                _ = ProcessMessageAsync(connectionId);
            }
            catch (Exception e)
            {
                // Fail to write initial open connection message to channel
                Log.ConnectedStartingFailed(_logger, connectionId, e);
                // Close channel and notify client to close connection
                clientContext.Output.TryComplete();
                await WriteAsync(new CloseConnectionMessage(connectionId, e.Message));
            }
        }

        protected override async Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            var connectionId = closeConnectionMessage.ConnectionId;
            if (_clientConnections.TryGetValue(connectionId, out var clientContext))
            {
                try
                {
                    await clientContext.Output.WriteAsync(closeConnectionMessage);
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
            if (_clientConnections.TryGetValue(connectionId, out var clientContext))
            {
                try
                {
                    await clientContext.Output.WriteAsync(connectionDataMessage);
                }
                catch (Exception e)
                {
                    Log.FailToWriteMessageToApplication(_logger, connectionId, e);
                }
            }
        }

        private void PerformDisconnectCore(string connectionId)
        {
            if (_clientConnections.TryRemove(connectionId, out var clientContext))
            {
                clientContext.Output.TryComplete();
                clientContext.Transport.OnDisconnected();
            }

            Log.ConnectedEnding(_logger, connectionId);
        }

        private async Task OnConnectedAsyncCore(ClientContext clientContext, OpenConnectionMessage message)
        {
            var connectionId = message.ConnectionId;
            try
            {
                clientContext.Transport = _clientConnectionManager.CreateConnection(message, this);
                Log.ConnectedStarting(_logger, connectionId);
            }
            catch (Exception e)
            {
                Log.ConnectedStartingFailed(_logger, connectionId, e);
                PerformDisconnectCore(connectionId);
                await WriteAsync(new CloseConnectionMessage(connectionId, e.Message));
            }
        }

        private void ProcessOutgoingMessages(ClientContext clientContext, ConnectionDataMessage connectionDataMessage)
        {
            var connectionId = connectionDataMessage.ConnectionId;
            try
            {
                var payload = connectionDataMessage.Payload;
                Log.WriteMessageToApplication(_logger, payload.Length, connectionId);
                var message = GetString(payload);
                if (message == ReconnectMessage)
                {
                    clientContext.Transport.Reconnected?.Invoke();
                }
                else
                {
                    clientContext.Transport.OnReceived(message);
                }
            }
            catch (Exception e)
            {
                Log.FailToWriteMessageToApplication(_logger, connectionDataMessage.ConnectionId, e);
            }
        }

        private async Task ProcessMessageAsync(string connectionId)
        {
            // Check if channel is created.
            if (_clientConnections.TryGetValue(connectionId, out var clientContext))
            {
                try
                {
                    // Check if channel is closed.
                    while (await clientContext.Input.WaitToReadAsync())
                    {
                        while (clientContext.Input.TryRead(out var serviceMessage))
                        {
                            switch (serviceMessage)
                            {
                                case OpenConnectionMessage openConnectionMessage:
                                    await OnConnectedAsyncCore(clientContext, openConnectionMessage);
                                    break;
                                case CloseConnectionMessage closeConnectionMessage:
                                    PerformDisconnectCore(closeConnectionMessage.ConnectionId);
                                    return;
                                case ConnectionDataMessage connectionDataMessage:
                                    ProcessOutgoingMessages(clientContext, connectionDataMessage);
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
                    PerformDisconnectCore(connectionId);
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
        
        private sealed class ClientContext
        {
            public ClientContext()
            {
                var channel = Channel.CreateUnbounded<ServiceMessage>();
                Input = channel.Reader;
                Output = channel.Writer;
            }

            public IServiceTransport Transport { get; set; }

            public ChannelReader<ServiceMessage> Input { get; }
            
            public ChannelWriter<ServiceMessage> Output { get; }
        }
    }
}
