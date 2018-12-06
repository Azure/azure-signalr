// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
        private readonly ConcurrentDictionary<string, ClientContext> _clientConnections = new ConcurrentDictionary<string, ClientContext>(StringComparer.Ordinal);

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
                    await PerformDisconnectCore(connection.Key, false);
                }
            }
            catch (Exception ex)
            {
                Log.FailedToCleanupConnections(_logger, ex);
            }
        }

        protected override async Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            // Create empty transport with only channel for async processing messages
            var connectionId = openConnectionMessage.ConnectionId;
            var clientContext = new ClientContext();
            try
            {
                await clientContext.Output.WriteAsync(openConnectionMessage);
                _clientConnections.TryAdd(connectionId, clientContext);

                // Writing from the application to the service
                clientContext.ApplicationTask = ProcessMessageAsync(connectionId, clientContext.CancellationToken.Token);
            }
            catch (Exception e)
            {
                // Fail to write initial open connection message to channel
                Log.ConnectedStartingFailed(_logger, connectionId, e);
                // Close channel and notify client to close connection
                await clientContext.DisposeAsync();
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

        private async Task PerformDisconnectCore(string connectionId, bool closeGracefully = true)
        {
            if (_clientConnections.TryRemove(connectionId, out var clientContext))
            {
                try
                {
                    await clientContext.DisposeAsync(closeGracefully);
                }
                catch (Exception e)
                {
                    Log.ApplicaitonTaskFailed(_logger, e);
                }
                finally
                {
                    clientContext.Transport.OnDisconnected();
                }
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
                await PerformDisconnectCore(connectionId);
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

        private async Task ProcessMessageAsync(string connectionId, CancellationToken token = default)
        {
            // Check if channel is created.
            if (_clientConnections.TryGetValue(connectionId, out var clientContext))
            {
                try
                {
                    // Check if channel is closed.
                    while (await clientContext.Input.WaitToReadAsync(token))
                    {
                        while (clientContext.Input.TryRead(out var serviceMessage))
                        {
                            switch (serviceMessage)
                            {
                                case OpenConnectionMessage openConnectionMessage:
                                    await OnConnectedAsyncCore(clientContext, openConnectionMessage);
                                    break;
                                case CloseConnectionMessage closeConnectionMessage:
                                    await PerformDisconnectCore(closeConnectionMessage.ConnectionId);
                                    break;
                                case ConnectionDataMessage connectionDataMessage:
                                    ProcessOutgoingMessages(clientContext, connectionDataMessage);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Current task is canceled in PerformDisconnectCore() and just igonore
                }
                catch (Exception e)
                {
                    // Internal exception is already catched and here only for channel exception.
                    // Notify client to disconnect.
                    Log.SendLoopStopped(_logger, connectionId, e);
                    await PerformDisconnectCore(connectionId, false);
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
                CancellationToken = new CancellationTokenSource();

                var channel = Channel.CreateUnbounded<ServiceMessage>();
                Input = channel.Reader;
                Output = channel.Writer;
            }

            public IServiceTransport Transport { get; set; }

            public ChannelReader<ServiceMessage> Input { get; }
            
            public ChannelWriter<ServiceMessage> Output { get; }

            public Task ApplicationTask { get; set; }

            public CancellationTokenSource CancellationToken { get; set; }

            public async Task DisposeAsync(bool closeGracefully = true)
            {
                try
                {
                    Output.Complete();
                    if (closeGracefully)
                    {
                        // Normally disconnect is the last message to process
                        // Mark channel complete and wait for application task to finish
                        var task = ApplicationTask ?? Task.CompletedTask;
                        await task;
                    }
                    else
                    {
                        // When disconnect is called for channel exception or cleanup connection
                        // No need to process rest messages, just cancel read to avoid hang in the task.
                        CancellationToken?.Cancel();
                    }
                }
                finally
                {
                    CancellationToken?.Dispose();
                    CancellationToken = null;
                }
            }
        }
    }
}
