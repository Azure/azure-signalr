// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Encoders;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.AspNetCore.Sockets;
using Microsoft.AspNetCore.Sockets.Client;
using Microsoft.AspNetCore.Sockets.Features;
using Microsoft.AspNetCore.Sockets.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    public class ServiceConnection<THub> where THub : Hub
    {
        private static readonly TimeSpan DefaultServerTimeout = TimeSpan.FromSeconds(30); // Server ping rate is 15 sec, this is 2 times that.
        private const string OnConnectedAsyncMethod = "onconnectedasync";
        private const string OnDisconnectedAsyncMethod = "ondisconnectedasync";

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IConnection _connection;
        private readonly IHubProtocol _protocol;
        private HubProtocolReaderWriter _protocolReaderWriter;

        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly HubDispatcher<THub> _hubDispatcher;
        private readonly HubConnectionList _connections = new HubConnectionList();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private CancellationTokenSource _connectionActive;

        //private int _nextId = 0;
        private volatile bool _startCalled;
        private readonly Timer _timeoutTimer;
        private bool _needKeepAlive;

        public TimeSpan ServerTimeout { get; set; } = DefaultServerTimeout;

        public ServiceConnection(IConnection connection,
            IHubProtocol protocol,
            HubLifetimeManager<THub> lifetimeManager,
            HubDispatcher<THub> hubDispatcher,
            ILoggerFactory loggerFactory)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<ServiceConnection<THub>>();
            _hubDispatcher = hubDispatcher;
            _lifetimeManager = lifetimeManager;

            // Create the timer for timeout, but disabled by default (we enable it when started).
            _timeoutTimer = new Timer(state => ((ServiceConnection<THub>)state).TimeoutElapsed(), this, Timeout.Infinite, Timeout.Infinite);

            connection.OnReceived((data, state) => ((ServiceConnection<THub>)state).OnDataReceivedAsync(data), this);
        }

        public async Task StartAsync()
        {
            try
            {
                await StartAsyncCore().ForceAsync();
            }
            finally
            {
                _startCalled = true;
            }

            //_writeTask = WriteToTransport();
        }

        private async Task StartAsyncCore()
        {
            var transferModeFeature = _connection.Features.Get<ITransferModeFeature>();
            if (transferModeFeature == null)
            {
                transferModeFeature = new TransferModeFeature();
                _connection.Features.Set(transferModeFeature);
            }

            var requestedTransferMode =
                _protocol.Type == ProtocolType.Binary
                    ? TransferMode.Binary
                    : TransferMode.Text;

            transferModeFeature.TransferMode = requestedTransferMode;
            await _connection.StartAsync();
            _needKeepAlive = _connection.Features.Get<IConnectionInherentKeepAliveFeature>() == null;

            var actualTransferMode = transferModeFeature.TransferMode;

            _protocolReaderWriter = new HubProtocolReaderWriter(_protocol, GetDataEncoder(requestedTransferMode, actualTransferMode));

            //_logger.HubProtocol(Protocol.Name);

            _connectionActive = new CancellationTokenSource();
            using (var memoryStream = new MemoryStream())
            {
                NegotiationProtocol.WriteMessage(new NegotiationMessage(_protocol.Name), memoryStream);
                await _connection.SendAsync(memoryStream.ToArray(), _connectionActive.Token);
            }

            //ResetTimeoutTimer();
        }

        private IDataEncoder GetDataEncoder(TransferMode requestedTransferMode, TransferMode actualTransferMode)
        {
            if (requestedTransferMode == TransferMode.Binary && actualTransferMode == TransferMode.Text)
            {
                // This is for instance for SSE which is a Text protocol and the user wants to use a binary
                // protocol so we need to encode messages.
                return new Base64Encoder();
            }

            Debug.Assert(requestedTransferMode == actualTransferMode, "All transports besides SSE are expected to support binary mode.");

            return new PassThroughEncoder();
        }

        public async Task StopAsync()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            await StopAsyncCore().ForceAsync();
        }

        private Task StopAsyncCore() => _connection.StopAsync();

        private void TimeoutElapsed()
        {
            _connection.AbortAsync(new TimeoutException($"Server timeout ({ServerTimeout.TotalMilliseconds:0.00}ms) elapsed without receiving a message from the server."));
        }

        private void ResetTimeoutTimer()
        {
            if (_needKeepAlive)
            {
                //_logger.ResettingKeepAliveTimer();
                _timeoutTimer.Change(ServerTimeout, Timeout.InfiniteTimeSpan);
            }
        }

        private async Task OnDataReceivedAsync(byte[] data)
        {
            if (!_startCalled)
            {
                throw new InvalidOperationException($"The '{nameof(OnDataReceivedAsync)}' method cannot be called before the connection has been started.");
            }

            if (_protocolReaderWriter.ReadMessages(data, _hubDispatcher, out var messages))
            {
                foreach (var message in messages)
                {
                    _ = DispatchAsync(message);
                }

                await Task.CompletedTask;
            }
        }

        private async Task DispatchAsync(HubMessage message)
        {
            var invocationMessage = message as HubInvocationMessage;
            if (invocationMessage == null)
            {
                throw new NotSupportedException($"Received unsupported message: {message}");
            }

            if (message is HubMethodInvocationMessage methodInvocationMessage1 &&
                OnConnectedAsyncMethod.Equals(methodInvocationMessage1.Target,
                    StringComparison.OrdinalIgnoreCase))
            {
                await OnConnectedAsync(invocationMessage);
            }

            var connection = GetHubConnectionContext(invocationMessage);
            if (connection == null)
            {
                await SendMessageAsync(CompletionMessage.WithError(invocationMessage.InvocationId, "No connection found."));
                return;
            }

            if (message is HubMethodInvocationMessage methodInvocationMessage2 &&
                OnDisconnectedAsyncMethod.Equals(methodInvocationMessage2.Target,
                    StringComparison.OrdinalIgnoreCase))
            {
                await OnDisconnectedAsync(connection, methodInvocationMessage2);
            }
            

            await _hubDispatcher.DispatchMessageAsync(connection, message);
        }

        private async Task OnConnectedAsync(HubInvocationMessage message)
        {
            var connection = CreateHubConnectionContext(message);
            _connections.Add(connection);

            await _lifetimeManager.OnConnectedAsync(connection);

            await _hubDispatcher.OnConnectedAsync(connection);

            await SendMessageAsync(CompletionMessage.WithResult(message.InvocationId, ""));
        }

        private async Task OnDisconnectedAsync(HubConnectionContext connection, HubInvocationMessage message)
        {
            await _hubDispatcher.OnDisconnectedAsync(connection, null);

            await _lifetimeManager.OnDisconnectedAsync(connection);

            await SendMessageAsync(CompletionMessage.WithResult(message.InvocationId, ""));

            _connections.Remove(connection);
        }

        private HubConnectionContext CreateHubConnectionContext(HubInvocationMessage message)
        {
            var context = CreateConnectionContext(message);
            // TODO: configurable KeepAliveInterval
            return new CloudHubConnectionContext(_connection, context, TimeSpan.FromSeconds(30), _loggerFactory)
                {
                    ProtocolReaderWriter = _protocolReaderWriter
                };
        }

        private DefaultConnectionContext CreateConnectionContext(HubInvocationMessage message)
        {
            var connectionId = message.GetConnectionId();
            // TODO:
            // No physical pipeline for logical ConnectionContext. These pipelines won't be used in current context.
            // So no exception or error will be thrown.
            // We should have a cleaner approach to reuse DefaultConnectionContext for Azure SignalR.
            var connectionContext = new DefaultConnectionContext(connectionId, null, null);
            if (message.TryGetClaims(out var claims))
            {
                connectionContext.User = new ClaimsPrincipal();
                connectionContext.User.AddIdentity(new ClaimsIdentity(claims));
            }
            return connectionContext;
        }

        private HubConnectionContext GetHubConnectionContext(HubInvocationMessage message)
        {
            return message.TryGetConnectionId(out var connectionId) ? _connections[connectionId] : null;
        }

        private async Task SendMessageAsync(HubMessage hubMessage)
        {
            var payload = _protocolReaderWriter.WriteMessage(hubMessage);
            // TODO:
            await _connection.SendAsync(payload, CancellationToken.None);
        }

        private class TransferModeFeature : ITransferModeFeature
        {
            public TransferMode TransferMode { get; set; }
        }
    }
}
