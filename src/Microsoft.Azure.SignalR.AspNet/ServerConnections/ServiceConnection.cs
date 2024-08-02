// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.AspNet;

internal partial class ServiceConnection : ServiceConnectionBase
{
    private const string ReconnectMessage = "asrs:reconnect";

    private static readonly Dictionary<string, string> CustomHeader = new Dictionary<string, string>
        {{Constants.AsrsUserAgent, ProductInfo.GetProductInfo()}};

    private static readonly TimeSpan CloseApplicationTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, ClientConnectionContext> _clientConnections =
        new ConcurrentDictionary<string, ClientConnectionContext>(StringComparer.Ordinal);

    private readonly IConnectionFactory _connectionFactory;

    private readonly IClientConnectionManagerAspNet _clientConnectionManager;

    private readonly AckHandler _ackHandler;

    public ServiceConnection(string serverId,
                             string connectionId,
                             HubServiceEndpoint endpoint,
                             IServiceProtocol serviceProtocol,
                             IConnectionFactory connectionFactory,
                             IClientConnectionManagerAspNet clientConnectionManager,
                             ILoggerFactory loggerFactory,
                             IServiceMessageHandler serviceMessageHandler,
                             IServiceEventHandler serviceEventHandler,
                             AckHandler ackHandler,
                             ServiceConnectionType connectionType = ServiceConnectionType.Default)
        : base(serviceProtocol,
               serverId,
               connectionId,
               endpoint,
               serviceMessageHandler,
               serviceEventHandler,
               clientConnectionManager,
               connectionType,
               loggerFactory?.CreateLogger<ServiceConnection>())
    {
        _connectionFactory = connectionFactory;
        _clientConnectionManager = clientConnectionManager;
        _ackHandler = ackHandler;
    }

    public override bool TryAddClientConnection(IClientConnection connection)
    {
        var r = _clientConnectionManager.TryAddClientConnection(connection);
        _clientConnections.TryAdd(connection.ConnectionId, (ClientConnectionContext)connection);
        return r;
    }

    public override bool TryRemoveClientConnection(string connectionId, out IClientConnection connection)
    {
        var r = _clientConnectionManager.TryRemoveClientConnection(connectionId, out connection);
        _clientConnections.TryRemove(connectionId, out _);
        return r;
    }

    protected override Task<ConnectionContext> CreateConnection(string target = null)
    {
        return _connectionFactory.ConnectAsync(HubEndpoint, TransferFormat.Binary, ConnectionId, target,
            headers: CustomHeader);
    }

    protected override Task DisposeConnection(ConnectionContext connection)
    {
        return _connectionFactory.DisposeAsync(connection);
    }

    /// <summary>
    /// Cleanup the client connections
    /// </summary>
    /// <param name="fromInstanceId">Specifies which Azure SignalR instance is the client connections come from, null means all</param>
    /// <returns></returns>
    protected override Task CleanupClientConnections(string fromInstanceId = null)
    {
        _ = CleanupConnectionsAsyncCore(fromInstanceId);
        return Task.CompletedTask;
    }

    protected override Task OnClientConnectedAsync(OpenConnectionMessage openConnectionMessage)
    {
        // Create empty transport with only channel for async processing messages
        var connectionId = openConnectionMessage.ConnectionId;
        var clientConnection = new ClientConnectionContext(openConnectionMessage)
        {
            ServiceConnection = this
        };

        var isDiagnosticClient = false;
        openConnectionMessage.Headers.TryGetValue(Constants.AsrsIsDiagnosticClient, out var isDiagnosticClientValue);
        if (!StringValues.IsNullOrEmpty(isDiagnosticClientValue))
        {
            isDiagnosticClient = Convert.ToBoolean(isDiagnosticClientValue.FirstOrDefault());
        }

        // todo: ignore asp.net for now
        using (new ClientConnectionScope(endpoint: HubEndpoint, outboundConnection: this, isDiagnosticClient: isDiagnosticClient))
        {
            if (_clientConnectionManager.TryAddClientConnection(clientConnection))
            {
                _clientConnections.TryAdd(connectionId, clientConnection);
                clientConnection.ApplicationTask = ProcessMessageAsync(clientConnection, clientConnection.CancellationToken);
                return ForwardMessageToApplication(connectionId, openConnectionMessage);
            }
            else
            {
                // the manager still contains this connectionId, probably this connection is not yet cleaned up
                Log.DuplicateConnectionId(Logger, connectionId, null);
                return SafeWriteAsync(
                    new CloseConnectionMessage(connectionId, $"Duplicate connection ID {connectionId}"));
            }
        }
    }

    protected override Task OnClientDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
    {
        return ForwardMessageToApplication(closeConnectionMessage.ConnectionId, closeConnectionMessage);
    }

    protected override Task OnClientMessageAsync(ConnectionDataMessage connectionDataMessage)
    {
        if (connectionDataMessage.TracingId != null)
        {
            MessageLog.ReceiveMessageFromService(Logger, connectionDataMessage);
        }
        return ForwardMessageToApplication(connectionDataMessage.ConnectionId, connectionDataMessage);
    }

    protected virtual async Task CleanupConnectionsAsyncCore(string instanceId = null)
    {
        try
        {
            var connectionIds = _clientConnections.Select(s => s.Key);
            if (!string.IsNullOrEmpty(instanceId))
            {
                connectionIds = _clientConnections.Where(s => s.Value.InstanceId == instanceId).Select(s => s.Key);
            }
            var tasks = connectionIds.Select(s => PerformDisconnectCore(s, true, false)).ToArray();
            if (tasks.Length > 0)
            {
                Log.ClosingClientConnections(Logger, tasks.Length, ConnectionId);
                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            Log.FailedToCleanupConnections(Logger, ex);
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
                Log.FailToWriteMessageToApplication(Logger, message.GetType().Name, connectionId, (message as IMessageWithTracingId)?.TracingId, e);
                _ = PerformDisconnectCore(connectionId, true);

                _ = SafeWriteAsync(new CloseConnectionMessage(connectionId, e.Message));
            }
        }
    }

    private async Task WaitForApplicationTask(ClientConnectionContext clientContext, bool closeGracefully)
    {
        clientContext.Output.TryComplete();
        var app = clientContext.ApplicationTask;
        if (!app.IsCompleted)
        {
            try
            {
                if (!closeGracefully)
                {
                    clientContext.CancelPendingRead();
                }

                using var delayCts = new CancellationTokenSource();

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
            catch (Exception ex)
            {
                Log.ApplicationTaskFailed(Logger, ex);
            }
        }
    }

    private async Task PerformDisconnectCore(string connectionId, bool waitForApplicationTask, bool closeGracefully = true)
    {
        // remove the connection from the global store so that a connection with the same connectionId can be added from elsewhere
        if (_clientConnectionManager.TryRemoveClientConnection(connectionId, out _))
        {
            if (_clientConnections.TryRemove(connectionId, out var clientContext))
            {
                if (waitForApplicationTask)
                {
                    await WaitForApplicationTask(clientContext, closeGracefully);
                }

                clientContext.Transport?.OnDisconnected();
                Log.ConnectedEnding(Logger, connectionId);
            }
        }
    }

    private async Task OnConnectedAsyncCore(ClientConnectionContext clientContext, OpenConnectionMessage message)
    {
        var connectionId = message.ConnectionId;
        try
        {
            clientContext.Transport = await _clientConnectionManager.CreateConnection(message);
            Log.ConnectedStarting(Logger, connectionId);
        }
        catch (Exception e)
        {
            Log.ConnectedStartingFailed(Logger, connectionId, e);

            // Should not wait for application task inside the application task
            _ = PerformDisconnectCore(connectionId, false);
            _ = SafeWriteAsync(new CloseConnectionMessage(connectionId, e.Message));
        }
    }

    private void ProcessOutgoingMessages(ClientConnectionContext clientContext, ConnectionDataMessage connectionDataMessage)
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
            Log.FailToWriteMessageToApplication(Logger, nameof(ConnectionDataMessage), connectionDataMessage.ConnectionId, connectionDataMessage.TracingId, e);
        }
    }

    private async Task ProcessMessageAsync(ClientConnectionContext clientContext, CancellationToken cancellation)
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
            _ = PerformDisconnectCore(connectionId, false);
            _ = SafeWriteAsync(new CloseConnectionMessage(connectionId, e.Message));
        }
    }
}
