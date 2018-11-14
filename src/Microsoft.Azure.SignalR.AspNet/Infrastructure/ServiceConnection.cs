// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal partial class ServiceConnection : ServiceConnectionBase
    {
        private const string ReconnectMessage = "asrs:reconnect";
        private readonly ConcurrentDictionary<string, AzureTransport> _clientConnections = new ConcurrentDictionary<string, AzureTransport>(StringComparer.Ordinal);

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

        protected override Task CleanupConnections()
        {
            try
            {
                foreach (var connection in _clientConnections)
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

        protected override Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            // Writing from the application to the service
            _ = OnConnectedAsyncCore(openConnectionMessage);

            return Task.CompletedTask;
        }

        protected override Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            var connectionId = closeConnectionMessage.ConnectionId;
            PerformDisconnectCore(connectionId);

            return Task.CompletedTask;
        }

        protected override Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            if (_clientConnections.TryGetValue(connectionDataMessage.ConnectionId, out var transport))
            {
                try
                {
                    var payload = connectionDataMessage.Payload;
                    Log.WriteMessageToApplication(_logger, payload.Length, connectionDataMessage.ConnectionId);
                    var message = GetString(payload);
                    if (message == ReconnectMessage)
                    {
                        transport.Reconnected?.Invoke();
                    }
                    else
                    {
                        transport.OnReceived(message);
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
            
            return Task.CompletedTask;
        }

        private void PerformDisconnectCore(string connectionId)
        {
            if (_clientConnections.TryRemove(connectionId, out var transport))
            {
                transport.OnDisconnected();
            }

            Log.ConnectedEnding(_logger, connectionId);
        }

        private Task OnConnectedAsyncCore(OpenConnectionMessage message)
        {
            var connectionId = message.ConnectionId;
            try
            {
                var transport = _clientConnectionManager.CreateConnection(message, this);
                _clientConnections.TryAdd(transport.ConnectionId, transport);
                Log.ConnectedStarting(_logger, connectionId);
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                Log.ConnectedStartingFailed(_logger, connectionId, e);
                PerformDisconnectCore(connectionId);
                return WriteAsync(new CloseConnectionMessage(connectionId, e.Message));
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
    }
}
