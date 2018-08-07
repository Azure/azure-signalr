// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal partial class ServiceConnection : ServiceConnectionBase
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly IClientConnectionFactory _clientConnectionFactory;
        private readonly IClientConnectionManager _clientConnectionManager;

        private readonly ConcurrentDictionary<string, string> _connectionIds =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private readonly ConnectionDelegate _connectionDelegate;

        public ServiceConnection(IServiceProtocol serviceProtocol,
                                 IClientConnectionManager clientConnectionManager,
                                 IConnectionFactory connectionFactory, 
                                 ILoggerFactory loggerFactory,
                                 ConnectionDelegate connectionDelegate,
                                 IClientConnectionFactory clientConnectionFactory,
                                 string connectionId) :
            base(serviceProtocol, loggerFactory.CreateLogger<ServiceConnection>(), connectionId)
        {
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _connectionDelegate = connectionDelegate;
            _clientConnectionFactory = clientConnectionFactory;
        }

        protected override Task<ConnectionContext> CreateConnection()
        {
            return _connectionFactory.ConnectAsync(TransferFormat.Binary, _connectionId);
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
                if (_connectionIds.Count == 0)
                {
                    return;
                }
                var tasks = new List<Task>(_connectionIds.Count);
                foreach (var connectionId in _connectionIds.Keys)
                {
                    tasks.Add(PerformDisconnectAsyncCore(connectionId));
                }
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Log.FailedToCleanupConnections(_logger, ex);
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
                        try
                        {
                            // Forward the message to the service
                            await WriteAsync(new ConnectionDataMessage(connection.ConnectionId, buffer));
                        }
                        catch (Exception ex)
                        {
                            Log.ErrorSendingMessage(_logger, ex);
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
                Log.SendLoopStopped(_logger, connection.ConnectionId, ex);
            }
            finally
            {
                connection.Application.Input.Complete();
            }
        }

        private void AddClientConnection(ServiceConnectionContext connection)
        {
            _clientConnectionManager.AddClientConnection(connection);
            _connectionIds.TryAdd(connection.ConnectionId, connection.ConnectionId);
        }

        private void RemoveClientConnection(string connectionId)
        {
            _clientConnectionManager.RemoveClientConnection(connectionId);
            _connectionIds.TryRemove(connectionId, out _);
        }

        protected override Task OnConnectedAsync(OpenConnectionMessage message)
        {
            var connection = _clientConnectionFactory.CreateConnection(message);
            AddClientConnection(connection);
            Log.ConnectedStarting(_logger, connection.ConnectionId);

            // Execute the application code
            connection.ApplicationTask = _connectionDelegate(connection);

            // Writing from the application to the service
            _ = ProcessOutgoingMessagesAsync(connection);

            // Waiting for the application to shutdown so we can clean up the connection
            _ = WaitOnApplicationTask(connection);
            return Task.CompletedTask;
        }

        private async Task WaitOnApplicationTask(ServiceConnectionContext connection)
        {
            Exception exception = null;

            try
            {
                // Wait for the application task to complete
                await connection.ApplicationTask;
            }
            catch (Exception ex)
            {
                // Capture the exception to communicate it to the transport (this isn't strictly required)
                exception = ex;
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
                var serviceMessage = new CloseConnectionMessage(connection.ConnectionId, errorMessage: "");
                await WriteAsync(serviceMessage);
                Log.CloseConnection(_logger, connection.ConnectionId);
            }
        }

        protected override Task PerformDisconnectAsync(CloseConnectionMessage closeConnectionMessage)
        {
            var connectionId = closeConnectionMessage.ConnectionId;
            return PerformDisconnectAsyncCore(connectionId);
        }

        private async Task PerformDisconnectAsyncCore(string connectionId)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var connection))
            {
                // Service already knows the client is closed, no need to be informed.
                connection.AbortOnClose = false;

                // We're done writing to the application output
                connection.Application.Output.Complete();

                // Wait on the application task to complete
                try
                {
                    await connection.ApplicationTask;
                }
                catch (Exception ex)
                {
                    Log.ApplicaitonTaskFailed(_logger, ex);
                }
            }
            // Close this connection gracefully then remove it from the list,
            // this will trigger the hub shutdown logic appropriately
            RemoveClientConnection(connectionId);
            Log.ConnectedEnding(_logger, connectionId);
        }

        protected override async Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionDataMessage.ConnectionId, out var connection))
            {
                try
                {
                    var payload = connectionDataMessage.Payload;
                    Log.WriteMessageToApplication(_logger, payload.Length, connectionDataMessage.ConnectionId);

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
                    Log.FailToWriteMessageToApplication(_logger, connectionDataMessage.ConnectionId, ex);
                }
            }
            else
            {
                // Unexpected error
                Log.ReceivedMessageForNonExistentConnection(_logger, connectionDataMessage.ConnectionId);
            }
        }
    }
}
