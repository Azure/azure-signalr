// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.AspNet;

internal class AzureTransport : IServiceTransport
{
    private readonly TaskCompletionSource<object> _lifetimeTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<object> _disconnectTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<object> _connectedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly HostContext _context;

    private readonly IMemoryPool _pool;

    private readonly JsonSerializer _serializer;

    private readonly IServiceProtocol _serviceProtocol;

    private readonly ILogger _logger;

    private readonly IClientConnectionManagerAspNet _clientConnectionManager;

    public Func<string, Task> Received { get; set; }

    public Func<Task> Connected { get; set; }

    public Func<Task> Reconnected { get; set; }

    public Func<bool, Task> Disconnected { get; set; }

    public string ConnectionId { get; set; }

    public Task WaitForConnected => _connectedTcs.Task;

    public Task LifeTimeTask => _lifetimeTcs.Task;

    public AzureTransport(HostContext context, IDependencyResolver resolver)
    {
        _context = context;
        context.Environment[AspNetConstants.Context.AzureSignalRTransportKey] = this;
        _pool = resolver.Resolve<IMemoryPool>();
        _serializer = resolver.Resolve<JsonSerializer>();
        _serviceProtocol = resolver.Resolve<IServiceProtocol>();
        _logger = resolver.Resolve<ILoggerFactory>()?.CreateLogger<AzureTransport>() ??
                  NullLogger<AzureTransport>.Instance;

        _clientConnectionManager = resolver.Resolve<IClientConnectionManagerAspNet>();
    }

    public Task<string> GetGroupsToken()
    {
        return Task.FromResult<string>(null);
    }

    public Task ProcessRequest(ITransportConnection connection)
    {
        _ = LifetimeExecute();
        return WaitForConnected;
    }

    public Task Send(object value)
    {
        if (_clientConnectionManager.TryGetClientConnection(ConnectionId, out var clientConnection))
        {
            var message = CreateConnectionDataMessage(ConnectionId, value, _serviceProtocol, _serializer, _pool);
            return clientConnection.ServiceConnection.WriteAsync(message);
        }

        // There is no need to throw when the connection is closed.
        Log.ConnectionNotFound(_logger, ConnectionId);
        return Task.CompletedTask;
    }

    public void OnReceived(string value)
    {
        _ = SafeInvokeReceived(value);
    }

    public void OnDisconnected() => _disconnectTcs.TrySetResult(null);

    private ConnectionDataMessage CreateConnectionDataMessage(string connectionId,
                                                                      object value,
                                                              IServiceProtocol protocol,
                                                              JsonSerializer serializer,
                                                              IMemoryPool pool)
    {
        using var writer = new MemoryPoolTextWriter(pool);
        serializer.Serialize(writer, value);
        writer.Flush();

        // Reuse ConnectionDataMessage to wrap the payload
        var wrapped = new ConnectionDataMessage(string.Empty, writer.Buffer);
        var message = new ConnectionDataMessage(connectionId, protocol.GetMessageBytes(wrapped));
        return message;
    }

    private async Task SafeInvokeReceived(string value)
    {
        try
        {
            var received = Received;
            if (received != null)
            {
                await received(value);
            }
        }
        catch (Exception e)
        {
            Log.InvokeReceivedEventFailed(_logger, ConnectionId, e);
        }
    }

    private async Task LifetimeExecute()
    {
        try
        {

            try
            {
                var connected = Connected;
                if (connected != null)
                {
                    Log.ExecutingConnected(_logger, ConnectionId);
                    await connected();
                    Log.ExecuteConnected(_logger, ConnectionId);
                }

                _connectedTcs.TrySetResult(null);
            }
            catch (Exception e)
            {
                Log.ErrorExecuteConnected(_logger, ConnectionId, e);
                _connectedTcs.TrySetException(e);
                throw;
            }

            await _disconnectTcs.Task;

            var disconnected = Disconnected;
            if (disconnected != null)
            {
                try
                {
                    Log.ExecutingDisconnected(_logger, ConnectionId);
                    await disconnected(true);
                    Log.ExecuteDisconnected(_logger, ConnectionId);
                }
                catch (Exception e)
                {
                    Log.ErrorExecuteDisconnected(_logger, ConnectionId, e);
                    throw;
                }
            }

            _lifetimeTcs.TrySetResult(null);
        }
        catch (Exception e)
        {
            _lifetimeTcs.TrySetException(e);
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception> ErrorExecuteConnectedAction =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "ErrorExecuteConnected"), "Error executing OnConnected in Hub for connection {TransportConnectionId}.");

        // Category: ServiceConnection
        private static readonly Action<ILogger, string, Exception> ErrorExecuteDisconnectedAction =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, "ErrorExecuteDisconnected"), "Error executing OnDisconnected in Hub for connection {TransportConnectionId}.");

        private static readonly Action<ILogger, string, Exception> ExecutingConnectedAction =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, "ExecutingConnected"), "Executing OnConnected in Hub for connection {TransportConnectionId}.");

        private static readonly Action<ILogger, string, Exception> ExecuteConnectedAction =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(4, "ExecuteConnected"), "Executed OnConnected in Hub for connection {TransportConnectionId}.");

        private static readonly Action<ILogger, string, Exception> ExecutingDisconnectedAction =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(5, "ExecutingDisconnected"), "Executing OnDisconnected in Hub for connection {TransportConnectionId}.");

        private static readonly Action<ILogger, string, Exception> ExecuteDisconnectedAction =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(6, "ExecuteDisconnected"), "Executed OnDisconnected in Hub for connection {TransportConnectionId}.");

        private static readonly Action<ILogger, string, Exception> ConnectionNotFoundAction =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(7, "ConnectionNotFound"), "Message was not sent because connection {TransportConnectionId} was not found.");

        private static readonly Action<ILogger, string, Exception> InvokeReceivedEventFailedAction =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(8, "InvokeReceivedEventFailed"), "Failed to invoke Receive event for connection {TransportConnectionId}.");

        public static void ErrorExecuteConnected(ILogger logger, string connectionId, Exception exception)
        {
            ErrorExecuteConnectedAction(logger, connectionId, exception);
        }

        public static void ErrorExecuteDisconnected(ILogger logger, string connectionId, Exception exception)
        {
            ErrorExecuteDisconnectedAction(logger, connectionId, exception);
        }

        public static void ExecuteConnected(ILogger logger, string connectionId)
        {
            ExecuteConnectedAction(logger, connectionId, null);
        }

        public static void ExecuteDisconnected(ILogger logger, string connectionId)
        {
            ExecuteDisconnectedAction(logger, connectionId, null);
        }
        public static void ExecutingConnected(ILogger logger, string connectionId)
        {
            ExecutingConnectedAction(logger, connectionId, null);
        }

        public static void ExecutingDisconnected(ILogger logger, string connectionId)
        {
            ExecutingDisconnectedAction(logger, connectionId, null);
        }
        public static void ConnectionNotFound(ILogger logger, string connectionId)
        {
            ConnectionNotFoundAction(logger, connectionId, null);
        }

        public static void InvokeReceivedEventFailed(ILogger logger, string connectionId, Exception exception)
        {
            InvokeReceivedEventFailedAction(logger, connectionId, exception);
        }
    }
}
