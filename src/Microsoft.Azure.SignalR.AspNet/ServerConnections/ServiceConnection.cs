﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private static readonly Dictionary<string, string> CustomHeader = new Dictionary<string, string> { { Constants.AsrsUserAgent, ProductInfo.GetProductInfo() } };
        private const string ReconnectMessage = "asrs:reconnect";

        private static readonly TimeSpan CloseApplicationTimeout = TimeSpan.FromSeconds(5);

        private readonly ConcurrentDictionary<string, ClientContext> _clientConnections = new ConcurrentDictionary<string, ClientContext>(StringComparer.Ordinal);
        private readonly IConnectionFactory _connectionFactory;
        private readonly IClientConnectionManager _clientConnectionManager;

        public ServiceConnection(
            string connectionId,
            HubServiceEndpoint endpoint,
            IServiceProtocol serviceProtocol,
            IConnectionFactory connectionFactory,
            IClientConnectionManager clientConnectionManager,
            ILoggerFactory loggerFactory,
            IServiceMessageHandler serviceMessageHandler,
            ServerConnectionType connectionType = ServerConnectionType.Default)
            : base(serviceProtocol, connectionId, endpoint, serviceMessageHandler, connectionType, loggerFactory?.CreateLogger<ServiceConnection>())
        {
            _connectionFactory = connectionFactory;
            _clientConnectionManager = clientConnectionManager;
        }

        protected override Task<ConnectionContext> CreateConnection(string target = null)
        {
            return _connectionFactory.ConnectAsync(HubEndpoint, TransferFormat.Binary, ConnectionId, target, headers: CustomHeader);
        }

        protected override Task DisposeConnection()
        {
            var connection = ConnectionContext;
            ConnectionContext = null;
            return _connectionFactory.DisposeAsync(connection);
        }

        protected override async Task CleanupConnections()
        {
            try
            {
                await Task.WhenAll(_clientConnections.Select(s => PerformDisconnectCore(s.Key, true)));
            }
            catch (Exception ex)
            {
                Log.FailedToCleanupConnections(Logger, ex);
            }
        }

        protected override Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            // Create empty transport with only channel for async processing messages
            var connectionId = openConnectionMessage.ConnectionId;
            var clientContext = new ClientContext(connectionId);

            if (_clientConnections.TryAdd(connectionId, clientContext) &&
                _clientConnectionManager.TryAdd(connectionId, this))
            {
                clientContext.ApplicationTask = ProcessMessageAsync(clientContext, clientContext.CancellationToken);
                return ForwardMessageToApplication(connectionId, openConnectionMessage);
            }
            else
            {
                Log.DuplicateConnectionId(Logger, connectionId, null);
                return Task.CompletedTask;
            }
        }

        protected override Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            return ForwardMessageToApplication(closeConnectionMessage.ConnectionId, closeConnectionMessage);
        }

        protected override Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            return ForwardMessageToApplication(connectionDataMessage.ConnectionId, connectionDataMessage);
        }

        private async Task ForwardMessageToApplication(string connectionId, ServiceMessage message)
        {
            if (_clientConnections.TryGetValue(connectionId, out var clientContext))
            {
                try
                {
                    await clientContext.Output.WriteAsync(message);
                }
                catch (Exception e)
                {
                    Log.FailToWriteMessageToApplication(Logger, message.GetType().Name, connectionId, e);
                    await PerformDisconnectCore(connectionId, true);

                    await SafeWriteAsync(new CloseConnectionMessage(connectionId, e.Message));
                }
            }
        }


        private async Task WaitForApplicationTask(ClientContext clientContext)
        {
            var app = clientContext.ApplicationTask;
            if (!app.IsCompleted)
            {
                try
                {
                    using (var delayCts = new CancellationTokenSource())
                    {
                        var resultTask =
                            await Task.WhenAny(app, Task.Delay(CloseApplicationTimeout, delayCts.Token));
                        if (resultTask != app)
                        {
                            // Application task timed out and it might never end writing to Transport.Output, cancel reading the pipe so that our ProcessOutgoing ends
                            clientContext.CancelPendingRead();
                            Log.ApplicationTaskTimedOut(Logger);
                        }
                        else
                        {
                            delayCts.Cancel();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.ApplicationTaskFailed(Logger, ex);
                }
            }
        }

        private async Task PerformDisconnectCore(string connectionId, bool waitForApplicationTask)
        {
            if (_clientConnections.TryRemove(connectionId, out var clientContext))
            {
                clientContext.Output.TryComplete();
                if (waitForApplicationTask)
                {
                    await WaitForApplicationTask(clientContext);
                }

                // remove the connection after application task completes
                _clientConnectionManager.TryRemoveServiceConnection(connectionId, out _);
                clientContext.Transport?.OnDisconnected();

                Log.ConnectedEnding(Logger, connectionId);
            }
        }

        private async Task OnConnectedAsyncCore(ClientContext clientContext, OpenConnectionMessage message)
        {
            var connectionId = message.ConnectionId;
            try
            {
                clientContext.Transport =
                    await _clientConnectionManager.CreateConnection(message, this);
                Log.ConnectedStarting(Logger, connectionId);
            }
            catch (Exception e)
            {
                Log.ConnectedStartingFailed(Logger, connectionId, e);
                // Should not wait for application task inside the application task
                await PerformDisconnectCore(connectionId, false);
                await SafeWriteAsync(new CloseConnectionMessage(connectionId, e.Message));
            }
        }

        private void ProcessOutgoingMessages(ClientContext clientContext, ConnectionDataMessage connectionDataMessage)
        {
            var connectionId = connectionDataMessage.ConnectionId;
            try
            {
                var payload = connectionDataMessage.Payload;
                Log.WriteMessageToApplication(Logger, payload.Length, connectionId);
                var message = GetString(payload);
                if (message == ReconnectMessage)
                {
                    clientContext.Transport?.Reconnected?.Invoke();
                }
                else
                {
                    clientContext.Transport?.OnReceived(message);
                }
            }
            catch (Exception e)
            {
                Log.FailToWriteMessageToApplication(Logger, nameof(ConnectionDataMessage), connectionDataMessage.ConnectionId, e);
            }
        }

        private async Task ProcessMessageAsync(ClientContext clientContext, CancellationToken cancellation)
        {
            var connectionId = clientContext.ConnectionId;
            try
            {
                // Check if channel is closed.
                while (await clientContext.Input.WaitToReadAsync(cancellation))
                {
                    while (clientContext.Input.TryRead(out var serviceMessage))
                    {
                        cancellation.ThrowIfCancellationRequested();

                        switch (serviceMessage)
                        {
                            case OpenConnectionMessage openConnectionMessage:
                                await OnConnectedAsyncCore(clientContext, openConnectionMessage);
                                break;
                            case CloseConnectionMessage closeConnectionMessage:
                                // should not wait for application task when inside the application task
                                // As the messages are in a queue, close message should be after all the other messages
                                await PerformDisconnectCore(closeConnectionMessage.ConnectionId, false);
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
            catch (OperationCanceledException e)
            {
                Log.SendLoopStopped(Logger, connectionId, e);
            }
            catch (Exception e)
            {
                // Internal exception is already caught and here only for channel exception.
                // Notify client to disconnect.
                Log.SendLoopStopped(Logger, connectionId, e);
                await PerformDisconnectCore(connectionId, false);
                await SafeWriteAsync(new CloseConnectionMessage(connectionId, e.Message));
            }
        }

        private async Task SafeWriteAsync(ServiceMessage message)
        {
            try
            {
                await WriteAsync(message);
            }
            catch { }
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
            private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

            public ClientContext(string connectionId)
            {
                ConnectionId = connectionId;
                var channel = Channel.CreateUnbounded<ServiceMessage>();
                Input = channel.Reader;
                Output = channel.Writer;
            }

            public Task ApplicationTask { get; set; }

            public CancellationToken CancellationToken => _cancellationTokenSource.Token;

            public void CancelPendingRead()
            {
                _cancellationTokenSource.Cancel();
            }

            public string ConnectionId { get; }

            public ChannelReader<ServiceMessage> Input { get; }
            
            public ChannelWriter<ServiceMessage> Output { get; }

            public IServiceTransport Transport { get; set; }
        }
    }
}
