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
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal partial class ServiceConnection : ServiceConnectionBase
    {
        private const int DefaultCloseTimeoutMilliseconds = 30000;

        private const string ClientConnectionCountInHub = "#clientInHub";

        private const string ClientConnectionCountInServiceConnection = "#client";

        // Fix issue: https://github.com/Azure/azure-signalr/issues/198
        // .NET Framework has restriction about reserved string as the header name like "User-Agent"
        private static readonly Dictionary<string, string> CustomHeader = new Dictionary<string, string> { { Constants.AsrsUserAgent, ProductInfo.GetProductInfo() } };

        private readonly IConnectionFactory _connectionFactory;

        private readonly IClientConnectionFactory _clientConnectionFactory;

        private readonly int _closeTimeOutMilliseconds;

        private readonly IClientConnectionManager _clientConnectionManager;

        private readonly ConcurrentDictionary<string, string> _connectionIds =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private readonly string[] _pingMessages =
            new string[4] { ClientConnectionCountInHub, null, ClientConnectionCountInServiceConnection, null };

        private readonly ConnectionDelegate _connectionDelegate;

        private readonly IClientInvocationManager _clientInvocationManager;

        private readonly AckHandler _ackHandler;

        public Action<HttpContext> ConfigureContext { get; set; }

        public ServiceConnection(IServiceProtocol serviceProtocol,
                                 IClientConnectionManager clientConnectionManager,
                                 IConnectionFactory connectionFactory,
                                 ILoggerFactory loggerFactory,
                                 ConnectionDelegate connectionDelegate,
                                 IClientConnectionFactory clientConnectionFactory,
                                 string serverId,
                                 string connectionId,
                                 HubServiceEndpoint endpoint,
                                 IServiceMessageHandler serviceMessageHandler,
                                 IServiceEventHandler serviceEventHandler,
                                 IClientInvocationManager clientInvocationManager,
                                 AckHandler ackHandler,
                                 ServiceConnectionType connectionType = ServiceConnectionType.Default,
                                 GracefulShutdownMode mode = GracefulShutdownMode.Off,
                                 int closeTimeOutMilliseconds = DefaultCloseTimeoutMilliseconds
            ) : base(serviceProtocol, serverId, connectionId, endpoint, serviceMessageHandler, serviceEventHandler, connectionType, loggerFactory?.CreateLogger<ServiceConnection>(), mode)
        {
            _clientConnectionManager = clientConnectionManager;
            _connectionFactory = connectionFactory;
            _connectionDelegate = connectionDelegate;
            _clientConnectionFactory = clientConnectionFactory;
            _closeTimeOutMilliseconds = closeTimeOutMilliseconds;
            _clientInvocationManager = clientInvocationManager;
            _ackHandler = ackHandler;
        }

        protected override Task<ConnectionContext> CreateConnection(string target = null)
        {
            return _connectionFactory.ConnectAsync(HubEndpoint, TransferFormat.Binary, ConnectionId, target, headers: CustomHeader);
        }

        protected override Task DisposeConnection(ConnectionContext connection)
        {
            return _connectionFactory.DisposeAsync(connection);
        }

        protected override Task CleanupClientConnections(string fromInstanceId = null)
        {
            // To gracefully complete client connections, let the client itself owns the connection lifetime

            foreach (var connection in _connectionIds)
            {
                if (!string.IsNullOrEmpty(fromInstanceId) && connection.Value != fromInstanceId)
                {
                    continue;
                }

                // We should not wait until all the clients' lifetime ends to restart another service connection
                _ = PerformDisconnectAsyncCore(connection.Key);
            }

            return Task.CompletedTask;
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

            if (message.Headers.TryGetValue(Constants.AsrsMigrateFrom, out var from))
            {
                connection.Features.Set<IConnectionMigrationFeature>(new ConnectionMigrationFeature(from, ServerId));
            }

            AddClientConnection(connection, message);

            bool isDiagnosticClient = false;
            message.Headers.TryGetValue(Constants.AsrsIsDiagnosticClient, out var isDiagnosticClientValue);
            if (!StringValues.IsNullOrEmpty(isDiagnosticClientValue))
            {
                isDiagnosticClient = Convert.ToBoolean(isDiagnosticClientValue.FirstOrDefault());
            }

            using (new ClientConnectionScope(endpoint: HubEndpoint, outboundConnection: this, isDiagnosticClient: isDiagnosticClient))
            {
                _ = ProcessClientConnectionAsync(connection);
            }

            if (connection.IsMigrated)
            {
                Log.MigrationStarting(Logger, connection.ConnectionId);
            }
            else
            {
                Log.ConnectedStarting(Logger, connection.ConnectionId);
            }

            return Task.CompletedTask;
        }

        protected override Task OnClientDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            var connectionId = closeConnectionMessage.ConnectionId;
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var context))
            {
                if (closeConnectionMessage.Headers.TryGetValue(Constants.AsrsMigrateTo, out var to))
                {
                    context.AbortOnClose = false;
                    context.Features.Set<IConnectionMigrationFeature>(new ConnectionMigrationFeature(ServerId, to));

                    // We have to prevent SignalR `{type: 7}` (close message) from reaching our client while doing migration.
                    // Since all data messages will be sent to `ServiceConnection` directly.
                    // We can simply ignore all messages came from the application.
                    context.CancelOutgoing();

                    // The close connection message must be the last message, so we could complete the pipe.
                    context.CompleteIncoming();
                }
            }
            return PerformDisconnectAsyncCore(connectionId);
        }

        protected override async Task OnClientMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            if (connectionDataMessage.TracingId != null)
            {
                MessageLog.ReceiveMessageFromService(Logger, connectionDataMessage);
            }
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionDataMessage.ConnectionId, out var connection))
            {
                try
                {
                    var payload = connectionDataMessage.Payload;
                    Log.WriteMessageToApplication(Logger, payload.Length, connectionDataMessage.ConnectionId);
                    await connection.WriteMessageAsync(payload);
                }
                catch (Exception ex)
                {
                    Log.FailToWriteMessageToApplication(Logger, connectionDataMessage, ex);
                }
            }
            else
            {
                // Unexpected error
                Log.ReceivedMessageForNonExistentConnection(Logger, connectionDataMessage);
            }
        }

        protected override Task DispatchMessageAsync(ServiceMessage message)
        {
            return message switch
            {
                PingMessage pingMessage => OnPingMessageAsync(pingMessage),
                ClientInvocationMessage clientInvocationMessage => OnClientInvocationAsync(clientInvocationMessage),
                ServiceMappingMessage serviceMappingMessage => OnServiceMappingAsync(serviceMappingMessage),
                ClientCompletionMessage clientCompletionMessage => OnClientCompletionAsync(clientCompletionMessage),
                ErrorCompletionMessage errorCompletionMessage => OnErrorCompletionAsync(errorCompletionMessage),
                _ => base.DispatchMessageAsync(message)
            };
        }

        protected override Task OnPingMessageAsync(PingMessage pingMessage)
        {
#if NET7_0_OR_GREATER
            if (RuntimeServicePingMessage.TryGetOffline(pingMessage, out var instanceId))
            {
                _clientInvocationManager.Caller.CleanupInvocationsByInstance(instanceId);

                // Router invocations will be cleanup by its `CleanupInvocationsByConnection`, which is called by `RemoveClientConnection`.
                // In `base.OnPingMessageAsync`, `CleanupClientConnections(instanceId)` will finally execute `RemoveClientConnection` for each ConnectionId.
            }
#endif
            return base.OnPingMessageAsync(pingMessage);
        }

        private async Task ProcessClientConnectionAsync(ClientConnectionContext connection)
        {
            try
            {
                // Writing from the application to the service
                var transport = ProcessOutgoingMessagesAsync(connection, connection.OutgoingAborted);

                // Waiting for the application to shutdown so we can clean up the connection
                var app = ProcessApplicationTaskAsyncCore(connection);

                var task = await Task.WhenAny(app, transport);

                // remove it from the connection list
                RemoveClientConnection(connection.ConnectionId);

                // This is the exception from application
                Exception exception = null;
                if (task == app)
                {
                    exception = app.Exception?.GetBaseException();

                    // there is no need to write to the transport as application is no longer running
                    Log.WaitingForTransport(Logger);

                    // app task completes connection.Transport.Output, which will completes connection.Application.Input and ends the transport
                    // Transports are written by us and are well behaved, wait for them to drain
                    connection.CancelOutgoing(_closeTimeOutMilliseconds);

                    // transport never throws
                    await transport;
                }
                else
                {
                    // transport task ends first, no data will be dispatched out
                    Log.WaitingForApplication(Logger);

                    try
                    {
                        // always wait for the application to complete
                        await app;
                    }
                    catch (Exception e)
                    {
                        exception = e;
                    }
                }

                if (exception != null)
                {
                    Log.ApplicationTaskFailed(Logger, exception);
                }

                // If we aren't already aborted, we send the abort message to the service
                if (connection.AbortOnClose)
                {
                    // Inform the Service that we will remove the client because SignalR told us it is disconnected.
                    var serviceMessage =
                        new CloseConnectionMessage(connection.ConnectionId, errorMessage: exception?.Message);

                    // when it fails, it means the underlying connection is dropped
                    // service is responsible for closing the client connections in this case and there is no need to throw
                    await SafeWriteAsync(serviceMessage);
                    Log.CloseConnection(Logger, connection.ConnectionId);
                }

                Log.ConnectedEnding(Logger, connection.ConnectionId);
            }
            catch (Exception e)
            {
                // When it throws, there must be something wrong
                Log.ProcessConnectionFailed(Logger, connection.ConnectionId, e);
            }
            finally
            {
                connection.OnCompleted();
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

        private async Task ProcessOutgoingMessagesAsync(ClientConnectionContext connection, CancellationToken token)
        {
            try
            {
                if (connection.IsMigrated)
                {
                    using var timeoutToken = new CancellationTokenSource(DefaultHandshakeTimeout);
                    using var source = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken.Token);

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
                    var result = await connection.Application.Input.ReadAsync(token);

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
                        catch (ServiceConnectionNotActiveException)
                        {
                            // Service connection not active means the transport layer for this connection is closed, no need to continue processing
                            break;
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
            }
        }

        private void AddClientConnection(ClientConnectionContext connection, OpenConnectionMessage message)
        {
            _clientConnectionManager.TryAddClientConnection(connection);
            _connectionIds.TryAdd(connection.ConnectionId, connection.InstanceId);
        }

        private async Task ProcessApplicationTaskAsyncCore(ClientConnectionContext connection)
        {
            Exception exception = null;

            try
            {
                // Wait for the application task to complete
                // application task can end when exception, or Context.Abort() from hub
                await _connectionDelegate(connection);
            }
            catch (ObjectDisposedException)
            {
                // When the application shuts down and disposes IServiceProvider, HubConnectionHandler.RunHubAsync is still running and runs into _dispatcher.OnDisconnectedAsync
                // no need to throw the error out
            }
            catch (Exception ex)
            {
                // Capture the exception to communicate it to the transport (this isn't strictly required)
                exception = ex;
                throw;
            }
            finally
            {
                // Close the transport side since the application is no longer running
                connection.Transport.Output.Complete(exception);
                connection.Transport.Input.Complete();
            }
        }

        private async Task PerformDisconnectAsyncCore(string connectionId)
        {
            var connection = RemoveClientConnection(connectionId);
            if (connection != null)
            {
                // In normal close, service already knows the client is closed, no need to be informed.
                connection.AbortOnClose = false;

                // We're done writing to the application output
                // Let the connection complete incoming
                connection.CompleteIncoming();

                // wait for the connection's lifetime task to end
                var lifetime = connection.LifetimeTask;

                // Wait on the application task to complete
                // We wait gracefully here to be consistent with self-host SignalR
                await Task.WhenAny(lifetime, Task.Delay(_closeTimeOutMilliseconds));

                if (!lifetime.IsCompleted)
                {
                    Log.DetectedLongRunningApplicationTask(Logger, connectionId);
                }

                await lifetime;
            }
        }

        private ClientConnectionContext RemoveClientConnection(string connectionId)
        {
            _connectionIds.TryRemove(connectionId, out _);
            _clientConnectionManager.TryRemoveClientConnection(connectionId, out var connection);
#if NET7_0_OR_GREATER
            _clientInvocationManager.CleanupInvocationsByConnection(connectionId);
#endif
            return connection;
        }

        private Task OnClientInvocationAsync(ClientInvocationMessage message)
        {
            _clientInvocationManager.Router.AddInvocation(message.ConnectionId, message.InvocationId, message.CallerServerId, default);
            return Task.CompletedTask;
        }

        private Task OnServiceMappingAsync(ServiceMappingMessage message)
        {
            _clientInvocationManager.Caller.AddServiceMapping(message);
            return Task.CompletedTask;
        }

        private Task OnClientCompletionAsync(ClientCompletionMessage clientCompletionMessage)
        {
            _clientInvocationManager.Caller.TryCompleteResult(clientCompletionMessage.ConnectionId, clientCompletionMessage);
            return Task.CompletedTask;
        }

        private Task OnErrorCompletionAsync(ErrorCompletionMessage errorCompletionMessage)
        {
            _clientInvocationManager.Caller.TryCompleteResult(errorCompletionMessage.ConnectionId, errorCompletionMessage);
            return Task.CompletedTask;
        }
    }
}
