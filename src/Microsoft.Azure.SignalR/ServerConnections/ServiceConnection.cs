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
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

internal partial class ServiceConnection : ServiceConnectionBase
{
    private const string ClientConnectionCountInHub = "#clientInHub";

    private const string ClientConnectionCountInServiceConnection = "#client";

    // Fix issue: https://github.com/Azure/azure-signalr/issues/198
    // .NET Framework has restriction about reserved string as the header name like "User-Agent"
    private static readonly Dictionary<string, string> CustomHeader = new Dictionary<string, string> { { Constants.AsrsUserAgent, ProductInfo.GetProductInfo() } };

    private readonly IConnectionFactory _connectionFactory;

    private readonly IClientConnectionFactory _clientConnectionFactory;

    private readonly IClientConnectionManager _clientConnectionManager;

    private readonly ConcurrentDictionary<string, string> _connectionIds =
        new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

    private readonly string[] _pingMessages =
        new string[4] { ClientConnectionCountInHub, null, ClientConnectionCountInServiceConnection, null };

    private readonly ConnectionDelegate _connectionDelegate;

    private readonly IClientInvocationManager _clientInvocationManager;

    private readonly IHubProtocolResolver _hubProtocolResolver;

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
                             IHubProtocolResolver hubProtocolResolver,
                             ServiceConnectionType connectionType = ServiceConnectionType.Default,
                             GracefulShutdownMode mode = GracefulShutdownMode.Off,
                             bool allowStatefulReconnects = false)
        : base(serviceProtocol,
               serverId,
               connectionId,
               endpoint,
               serviceMessageHandler,
               serviceEventHandler,
               clientConnectionManager,
               connectionType,
               loggerFactory?.CreateLogger<ServiceConnection>(),
               mode,
               allowStatefulReconnects)
    {
        _clientConnectionManager = clientConnectionManager;
        _connectionFactory = connectionFactory;
        _connectionDelegate = connectionDelegate;
        _clientConnectionFactory = clientConnectionFactory;
        _clientInvocationManager = clientInvocationManager;
        _hubProtocolResolver = hubProtocolResolver;
    }

    public override bool TryAddClientConnection(IClientConnection connection)
    {
        var r = _clientConnectionManager.TryAddClientConnection(connection);
        _connectionIds.TryAdd(connection.ConnectionId, connection.InstanceId);
        return r;
    }

    public override bool TryRemoveClientConnection(string connectionId, out IClientConnection connection)
    {
        _connectionIds.TryRemove(connectionId, out _);
        var r = _clientConnectionManager.TryRemoveClientConnection(connectionId, out connection);
#if NET7_0_OR_GREATER
        _clientInvocationManager.CleanupInvocationsByConnection(connectionId);
#endif
        return r;
    }

    protected override Task<ConnectionContext> CreateConnection(string target = null)
    {
        return _connectionFactory.ConnectAsync(HubEndpoint, TransferFormat.Binary, ConnectionId, target, headers: CustomHeader);
    }

    protected override Task DisposeConnection(ConnectionContext connection)
    {
        return _connectionFactory.DisposeAsync(connection);
    }

    public override async Task CloseClientConnections(CancellationToken token)
    {
        var tasks = new List<Task>();
        foreach (var entity in _connectionIds)
        {
            if (_clientConnectionManager.TryGetClientConnection(entity.Key, out var c) && c is ClientConnectionContext connection)
            {
                // batch remove 100 connections once
                if (tasks.Count % 100 == 0)
                {
                    await Task.Delay(1);
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
                // Add a little delay to avoid too much pressure closing all the clients at one time
                tasks.Add(connection.PerformDisconnectAsync());
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }

    protected override Task CleanupClientConnections(string fromInstanceId = null)
    {
        // To gracefully complete client connections, let the client itself owns the connection lifetime

        foreach (var entity in _connectionIds)
        {
            if (!string.IsNullOrEmpty(fromInstanceId) && entity.Value != fromInstanceId)
            {
                continue;
            }

            if (_clientConnectionManager.TryRemoveClientConnection(entity.Key, out var c) && c is ClientConnectionContext connection)
            {
                // We should not wait until all the clients' lifetime ends to restart another service connection
                _ = connection.PerformDisconnectAsync();
            }
        }

        return Task.CompletedTask;
    }

    protected override ReadOnlyMemory<byte> GetPingMessage()
    {
        _pingMessages[1] = _clientConnectionManager.Count.ToString();
        _pingMessages[3] = _connectionIds.Count.ToString();

        return ServiceProtocol.GetMessageBytes(
            new PingMessage
            {
                Messages = _pingMessages
            });
    }

    protected override Task OnClientConnectedAsync(OpenConnectionMessage message)
    {
        var connection = _clientConnectionFactory.CreateConnection(message, ConfigureContext) as ClientConnectionContext;
        connection.ServiceConnection = this;

        connection.Features.Set<IConnectionMigrationFeature>(null);
        if (message.Headers.TryGetValue(Constants.AsrsMigrateFrom, out var from))
        {
            connection.Features.Set<IConnectionMigrationFeature>(new ConnectionMigrationFeature(from, ServerId));
        }

        TryAddClientConnection(connection);

        var isDiagnosticClient = false;
        message.Headers.TryGetValue(Constants.AsrsIsDiagnosticClient, out var isDiagnosticClientValue);
        if (!StringValues.IsNullOrEmpty(isDiagnosticClientValue))
        {
            isDiagnosticClient = Convert.ToBoolean(isDiagnosticClientValue.FirstOrDefault());
        }

        using (new ClientConnectionScope(endpoint: HubEndpoint, outboundConnection: this, isDiagnosticClient: isDiagnosticClient))
        {
            SignalRProtocol.IHubProtocol protocol;
            if (message.Protocol == FakeBlazorPackHubProtocol.Instance.Name)
            {
                // hack: blazorpack cannot parse it correctly, we use a fake one to workaround it.
                protocol = FakeBlazorPackHubProtocol.Instance;
            }
            else
            {
                protocol = _hubProtocolResolver.GetProtocol(message.Protocol, null);
            }
            _ = ProcessClientConnectionAsync(connection, protocol);
        }

        return Task.CompletedTask;
    }

    protected override Task OnClientDisconnectedAsync(CloseConnectionMessage message)
    {
        if (_clientConnectionManager.TryRemoveClientConnection(message.ConnectionId, out var c) && c is ClientConnectionContext connection)
        {
            connection.Features.Set<IConnectionMigrationFeature>(null);

            if (message.Headers.TryGetValue(Constants.AsrsMigrateTo, out var to))
            {
                connection.AbortOnClose = false;
                connection.Features.Set<IConnectionMigrationFeature>(new ConnectionMigrationFeature(ServerId, to));

                // We have to prevent SignalR `{type: 7}` (close message) from reaching our client while doing migration.
                // Since all data messages will be sent to `ServiceConnection` directly.
                // We can simply ignore all messages came from the application.
                connection.CancelOutgoing();

                // The close connection message must be the last message, so we could complete the pipe.
                connection.CompleteIncoming();
            }
            return connection.PerformDisconnectAsync();
        }
        return Task.CompletedTask;
    }

    protected override async Task OnClientMessageAsync(ConnectionDataMessage connectionDataMessage)
    {
        if (connectionDataMessage.TracingId != null)
        {
            MessageLog.ReceiveMessageFromService(Logger, connectionDataMessage);
        }

        if (_clientConnectionManager.TryGetClientConnection(connectionDataMessage.ConnectionId, out var connection))
        {
            await (connection as ClientConnectionContext).ProcessConnectionDataMessageAsync(connectionDataMessage);
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
            ConnectionReconnectMessage connectionReconnectMessage => OnConnectionReconnectAsync(connectionReconnectMessage),
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

    private async Task ProcessClientConnectionAsync(ClientConnectionContext connection, SignalRProtocol.IHubProtocol protocol)
    {
        try
        {
            // Writing from the application to the service
            var transport = connection.ProcessOutgoingMessagesAsync(protocol);

            // Waiting for the application to shutdown so we can clean up the connection
            var app = connection.ProcessApplicationAsync(_connectionDelegate);

            var task = await Task.WhenAny(app, transport);

            // remove it from the connection list
            TryRemoveClientConnection(connection.ConnectionId, out _);

            // This is the exception from application
            Exception exception = null;
            if (task == app)
            {
                exception = app.Exception?.GetBaseException();

                // there is no need to write to the transport as application is no longer running
                Log.WaitingForTransport(Logger);

                // app task completes connection.Transport.Output, which will completes connection.Application.Input and ends the transport
                // Transports are written by us and are well behaved, wait for them to drain
                connection.CancelOutgoing(true);
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
                var closeConnectionMessage = new CloseConnectionMessage(connection.ConnectionId, errorMessage: exception?.Message);

                // when it fails, it means the underlying connection is dropped
                // service is responsible for closing the client connections in this case and there is no need to throw
                await SafeWriteAsync(closeConnectionMessage);
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

    private Task OnConnectionReconnectAsync(ConnectionReconnectMessage message)
    {
        if (_clientConnectionManager.TryGetClientConnection(message.ConnectionId, out var connection))
        {
            (connection as ClientConnectionContext)?.ClearBufferedMessages();
        }
        return Task.CompletedTask;
    }

}
