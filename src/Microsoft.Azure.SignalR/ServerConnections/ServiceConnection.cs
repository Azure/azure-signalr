// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal partial class ServiceConnection : ServiceConnectionBase
    {
        // Fix issue: https://github.com/Azure/azure-signalr/issues/198
        // .NET Framework has restriction about reserved string as the header name like "User-Agent"
        private static readonly Dictionary<string, string> CustomHeader = new Dictionary<string, string> { { Constants.AsrsUserAgent, ProductInfo.GetProductInfo() } };
        private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

        private readonly int _migrationLevel = 0;

        private bool MigrationEnabled => _migrationLevel > 0;

        private const string ClientConnectionCountInHub = "#clientInHub";
        private const string ClientConnectionCountInServiceConnection = "#client";

        private readonly IConnectionFactory _connectionFactory;
        private readonly IClientConnectionFactory _clientConnectionFactory;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly ConcurrentDictionary<string, string> _connectionIds =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private readonly string[] _pingMessages =
            new string[4] { ClientConnectionCountInHub, null, ClientConnectionCountInServiceConnection, null };

        private readonly ConnectionDelegate _connectionDelegate;

        public Action<HttpContext> ConfigureContext { get; set; }

        public ServiceConnection(IServiceProtocol serviceProtocol,
                                 IClientConnectionManager clientConnectionManager,
                                 IConnectionFactory connectionFactory,
                                 ILoggerFactory loggerFactory,
                                 ConnectionDelegate connectionDelegate,
                                 IClientConnectionFactory clientConnectionFactory,
                                 string connectionId,
                                 HubServiceEndpoint endpoint,
                                 IServiceMessageHandler serviceMessageHandler,
                                 ServerConnectionType connectionType = ServerConnectionType.Default) :
            base(serviceProtocol, connectionId, endpoint, serviceMessageHandler, connectionType, loggerFactory?.CreateLogger<ServiceConnection>())
        {
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _connectionDelegate = connectionDelegate;
            _clientConnectionFactory = clientConnectionFactory;
        }

        protected override Task<ConnectionContext> CreateConnection(string target = null)
        {
            return _connectionFactory.ConnectAsync(HubEndpoint, TransferFormat.Binary, ConnectionId, target, headers: CustomHeader);
        }

        protected override Task DisposeConnection(ConnectionContext connection)
        {
            return _connectionFactory.DisposeAsync(connection);
        }

        protected override async Task CleanupClientConnections(string fromInstanceId = null)
        {
            try
            {
                if (_connectionIds.Count == 0)
                {
                    return;
                }
                var connectionIds = _connectionIds.Select(s => s.Key);
                if (!string.IsNullOrEmpty(fromInstanceId))
                {
                    connectionIds = _connectionIds.Where(s => s.Value == fromInstanceId).Select(s => s.Key);
                }
                await Task.WhenAll(connectionIds.Select(async (connectionId) =>
                {
                    if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var conn))
                    {
                        conn.AbortOnClose = false;
                        await PerformDisconnectAsyncCore(conn);
                    }
                }));
            }
            catch (Exception ex)
            {
                Log.FailedToCleanupConnections(Logger, ex);
            }
        }

        protected override ReadOnlyMemory<byte> GetPingMessage()
        {
            _pingMessages[1] = _clientConnectionManager.ClientConnections.Count.ToString();
            _pingMessages[3] = _connectionIds.Count.ToString();

            return ServiceProtocol.GetMessageBytes(
                new PingMessage
                {
                    Messages = _pingMessages
                });
        }

        protected override Task OnClientConnectedAsync(OpenConnectionMessage message)
        {
            var connection = _clientConnectionFactory.CreateConnection(message, ConfigureContext);
            connection.ServiceConnection = this;
            AddClientConnection(connection, message);

            if (connection.IsMigrated)
            {
                Log.MigrationStarting(Logger, connection.ConnectionId);
            }
            else
            {
                Log.ConnectedStarting(Logger, connection.ConnectionId);
            }

            // Execute the application code
            connection.ApplicationTask = _connectionDelegate(connection);

            // Writing from the application to the service
            _ = ProcessOutgoingMessagesAsync(connection);

            // Waiting for the application to shutdown so we can clean up the connection
            _ = WaitOnApplicationTask(connection);

            return Task.CompletedTask;
        }

        protected override Task OnClientDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            var connectionId = closeConnectionMessage.ConnectionId;
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var connection))
            {
                connection.AbortOnClose = false;
                if (MigrationEnabled)
                {
                    connection.HttpContext.Request.Headers[Constants.AsrsMigrateOut] = "";
                    // We have to prevent SignalR `{type: 7}` (close message) from reaching our client while doing migration.
                    // Since all user-created messages will be sent to `ServiceConnection` directly.
                    // We can simply ignore all messages came from the application pipe.
                    connection.Application.Input.CancelPendingRead();
                }
                return PerformDisconnectAsyncCore(connection);
            }
            return Task.CompletedTask;
        }

        protected override async Task OnClientMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionDataMessage.ConnectionId, out var connection))
            {
                try
                {
                    var payload = connectionDataMessage.Payload;
                    Log.WriteMessageToApplication(Logger, payload.Length, connectionDataMessage.ConnectionId);

                    if (payload.IsSingleSegment)
                    {
                        // Write the raw connection payload to the pipe let the upstream handle it
                        await connection.Application.Output.WriteAsync(payload.First);
                    }
                    else
                    {
                        var position = payload.Start;
                        while (connectionDataMessage.Payload.TryGet(ref position, out var memory))
                        {
                            await connection.Application.Output.WriteAsync(memory);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.FailToWriteMessageToApplication(Logger, connectionDataMessage.ConnectionId, ex);
                }
            }
            else
            {
                // Unexpected error
                Log.ReceivedMessageForNonExistentConnection(Logger, connectionDataMessage.ConnectionId);
            }
        }

        private async Task<bool> SkipHandshakeResponse(ClientConnectionContext connection, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    var result = await connection.Application.Input.ReadAsync(token);
                    if (result.IsCanceled || token.IsCancellationRequested)
                    {
                        return false;
                    }

                    var buffer = result.Buffer;
                    if (buffer.IsEmpty)
                    {
                        continue;
                    }

                    if (SignalRProtocol.HandshakeProtocol.TryParseResponseMessage(ref buffer, out var message))
                    {
                        connection.Application.Input.AdvanceTo(buffer.Start);
                        return true;
                    }

                    if (result.IsCompleted)
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorSkippingHandshakeResponse(Logger, ex);
            }
            return false;
        }

        private async Task ProcessOutgoingMessagesAsync(ClientConnectionContext connection)
        {
            try
            {
                if (connection.IsMigrated)
                {
                    using var source = new CancellationTokenSource(DefaultHandshakeTimeout);

                    // A handshake response is not expected to be given
                    // if the connection was migrated from another server, 
                    // since the connection hasn't been `dropped` from the client point of view.
                    if (!await SkipHandshakeResponse(connection, source.Token))
                    {
                        return;
                    }
                }

                while (true)
                {
                    var result = await connection.Application.Input.ReadAsync();

                    if (result.IsCanceled)
                    {
                        break;
                    }

                    var buffer = result.Buffer;

                    if (!buffer.IsEmpty) 
                    {
                        try
                        {
                            // Forward the message to the service
                            await WriteAsync(new ConnectionDataMessage(connection.ConnectionId, buffer));
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorSendingMessage(Logger, ex);
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // This connection ended (the application itself shut down) we should remove it from the list of connections
                        break;
                    }

                    connection.Application.Input.AdvanceTo(buffer.End);
                }
            }
            catch (Exception ex)
            {
                // The exception means application fail to process input anymore
                // Cancel any pending flush so that we can quit and perform disconnect
                // Here is abort close and WaitOnApplicationTask will send close message to notify client to disconnect
                Log.SendLoopStopped(Logger, connection.ConnectionId, ex);
                connection.Application.Output.CancelPendingFlush();
            }
            finally
            {
                connection.Application.Input.Complete();
                await PerformDisconnectAsyncCore(connection);
                connection.OnCompleted();
            }
        }

        private void AddClientConnection(ClientConnectionContext connection, OpenConnectionMessage message)
        {
            var instanceId = GetInstanceId(message.Headers);
            _clientConnectionManager.AddClientConnection(connection);
            _connectionIds.TryAdd(connection.ConnectionId, instanceId);
        }

        private async Task WaitOnApplicationTask(ClientConnectionContext connection)
        {
            Exception exception = null;

            try
            {
                // Wait for the application task to complete
                // application task can end when exception, or Context.Abort() from hub
                await connection.ApplicationTask;
            }
            catch (Exception ex)
            {
                // Capture the exception to communicate it to the transport (this isn't strictly required)
                exception = ex;
                Log.ApplicationTaskFailed(Logger, ex);
            }
            finally
            {
                // Close the transport side since the application is no longer running
                connection.Transport.Output.Complete(exception);
                connection.Transport.Input.Complete();
            }

            // If we aren't already aborted, we send the abort message to the service
            if (connection.AbortOnClose)
            {
                // Inform the Service that we will remove the client because SignalR told us it is disconnected.
                var serviceMessage = new CloseConnectionMessage(connection.ConnectionId, errorMessage: exception?.Message);
                await WriteAsync(serviceMessage);
                Log.CloseConnection(Logger, connection.ConnectionId);
            }
        }

        private async Task PerformDisconnectAsyncCore(ClientConnectionContext connection)
        {
            if (connection == null || _clientConnectionManager.RemoveClientConnection(connection.ConnectionId) == null)
            {
                return;
            }

            // remove it from the list to prevent it from called multiple times
            _connectionIds.TryRemove(connection.ConnectionId, out _);

            // We're done writing to the application output
            connection.Application.Output.Complete();

            var app = connection.ApplicationTask;

            // Wait on the application task to complete
            if (!app.IsCompleted)
            {
                try
                {
                    using (var delayCts = new CancellationTokenSource())
                    {
                        var resultTask =
                            await Task.WhenAny(app, Task.Delay(CloseTimeout, delayCts.Token));
                        if (resultTask != app)
                        {
                            // Application task timed out and it might never end writing to Transport.Output, cancel reading the pipe so that our ProcessOutgoing ends
                            connection.Application.Input.CancelPendingRead();
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

            Log.ConnectedEnding(Logger, connection.ConnectionId);
        }

        private string GetInstanceId(IDictionary<string, StringValues> header)
        {
            if (header.TryGetValue(Constants.AsrsInstanceId, out var instanceId))
            {
                return instanceId;
            }
            return string.Empty;
        }
    }
}
