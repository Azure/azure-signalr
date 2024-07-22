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

    private readonly TaskCompletionSource<object> _connectedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly HostContext _context;

    private readonly IMemoryPool _pool;

    private readonly JsonSerializer _serializer;

    private readonly IServiceProtocol _serviceProtocol;

    private readonly ILogger _logger;

    private readonly IClientConnectionManager _clientConnectionManager;

    public Func<string, Task> Received { get; set; }

    public Func<Task> Connected { get; set; }

    public Func<Task> Reconnected { get; set; }

    public Func<bool, Task> Disconnected { get; set; }

    public string ConnectionId { get; set; }

    public Task WaitForConnected => _connectedTcs.Task;

    public AzureTransport(HostContext context, IDependencyResolver resolver)
    {
        _context = context;
        context.Environment[AspNetConstants.Context.AzureSignalRTransportKey] = this;
        _pool = resolver.Resolve<IMemoryPool>();
        _serializer = resolver.Resolve<JsonSerializer>();
        _serviceProtocol = resolver.Resolve<IServiceProtocol>();
        _logger = resolver.Resolve<ILoggerFactory>()?.CreateLogger<AzureTransport>() ??
                  NullLogger<AzureTransport>.Instance;

        _clientConnectionManager = resolver.Resolve<IClientConnectionManager>();
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
        if (_clientConnectionManager.TryGetClientConnection(ConnectionId, out var connection))
        {
            var message = CreateConnectionDataMessage(connection.ConnectionId, value, _serviceProtocol, _serializer, _pool);
            return connection.ServiceConnection.WriteAsync(message);
        }
        throw new InvalidOperationException("No service connection found when sending message");
    }

    public void OnReceived(string value)
    {
        var received = Received;
        if (received != null)
        {
            // TODO: Add log
            _ = received(value);
        }
    }

    public void OnDisconnected() => _lifetimeTcs.TrySetResult(null);

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

    private async Task LifetimeExecute()
    {
        try
        {
            var connected = Connected;
            if (connected != null)
            {
                await connected();
            }

            _connectedTcs.TrySetResult(null);
        }
        catch (Exception e)
        {
            Log.ErrorExecuteConnected(_logger, ConnectionId, e);
            _connectedTcs.TrySetException(e);
            throw;
        }

        await _lifetimeTcs.Task;

        var disconnected = Disconnected;
        if (disconnected != null)
        {
            try
            {
                await disconnected(true);
            }
            catch (Exception e)
            {
                Log.ErrorExecuteDisconnected(_logger, ConnectionId, e);
                throw;
            }
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception> _errorExecuteConnected =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "ErrorExecuteConnected"), "Error executing OnConnected in Hub for connection {TransportConnectionId}.");

        // Category: ServiceConnection
        private static readonly Action<ILogger, string, Exception> _errorExecuteDisconnected =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(2, "ErrorExecuteDisconnected"), "Error executing OnDisconnected in Hub for connection {TransportConnectionId}.");

        public static void ErrorExecuteConnected(ILogger logger, string connectionId, Exception exception)
        {
            _errorExecuteConnected(logger, connectionId, exception);
        }

        public static void ErrorExecuteDisconnected(ILogger logger, string connectionId, Exception exception)
        {
            _errorExecuteDisconnected(logger, connectionId, exception);
        }
    }
}