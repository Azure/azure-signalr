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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    public class CloudConnection<THub> where THub : Hub
    {
        private const int MaxReconnectInterval = 1000; // in milliseconds
        private const string OnConnectedAsyncMethod = "onconnectedasync";
        private const string OnDisconnectedAsyncMethod = "ondisconnectedasync";
        private const string ConnectCallback = "HubHostOptions.OnConnected";
        private const string DisconnectCallback = "HubHostOptions.OnDisconnected";

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IConnection _connection;
        private readonly HubHostOptions _options;
        private readonly IHubProtocol _protocol;
        private HubProtocolReaderWriter _protocolReaderWriter;

        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly HubDispatcher<THub> _hubDispatcher;
        private readonly HubConnectionList _connections = new HubConnectionList();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // TODO: expose connection status to user code
        private volatile bool _isConnected;
        private readonly Timer _timeoutTimer;
        private bool _needKeepAlive;

        private readonly Timer _reconnectTimer;
        private int _reconnectIntervalInMS => StaticRandom.Next(MaxReconnectInterval);
        
        public CloudConnection(IConnection connection,
            IHubProtocol protocol,
            HubHostOptions options,
            HubLifetimeManager<THub> lifetimeManager,
            HubDispatcher<THub> hubDispatcher,
            ILoggerFactory loggerFactory)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _lifetimeManager = lifetimeManager ?? throw new ArgumentNullException(nameof(lifetimeManager));
            _hubDispatcher = hubDispatcher ?? throw new ArgumentNullException(nameof(hubDispatcher));

            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<CloudConnection<THub>>();

            _timeoutTimer = new Timer(state => ((CloudConnection<THub>) state).TimeoutElapsed(), this, Timeout.Infinite,
                Timeout.Infinite);

            _reconnectTimer =
                new Timer(state => ((CloudConnection<THub>) state).StartAsync().GetAwaiter().GetResult(), this,
                    Timeout.Infinite, Timeout.Infinite);

            connection.OnReceived((data, state) => ((CloudConnection<THub>) state).OnDataReceivedAsync(data), this);
            connection.Closed += OnHttpConnectionClosed;
        }

        public async Task StartAsync()
        {
            try
            {
                await StartAsyncCore();
                _isConnected = true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to connect to Azure SignalR due to error: {ex.Message}");
                ResetReconnectTimer();
                return;
            }

            await RunUserCallbackAsync(ConnectCallback, _options.OnConnected);
        }

        private async Task StartAsyncCore()
        {
            var transferModeFeature = GetOrAddTransferModeFeature();
            var requestedMode = transferModeFeature.TransferMode;

            await _connection.StartAsync();

            var actualMode = transferModeFeature.TransferMode;
            _protocolReaderWriter = GetProtocolReaderWriter(requestedMode, actualMode);

            await NegotiateProtocol();

            _needKeepAlive = _connection.Features.Get<IConnectionInherentKeepAliveFeature>() == null;
            ResetTimeoutTimer();
        }

        private ITransferModeFeature GetOrAddTransferModeFeature()
        {
            var transferModeFeature = _connection.Features.Get<ITransferModeFeature>();
            if (transferModeFeature == null)
            {
                transferModeFeature = new TransferModeFeature();
                _connection.Features.Set(transferModeFeature);
            }

            transferModeFeature.TransferMode = _protocol.Type == ProtocolType.Binary
                ? TransferMode.Binary
                : TransferMode.Text;

            return transferModeFeature;
        }

        private HubProtocolReaderWriter GetProtocolReaderWriter(TransferMode requestedMode, TransferMode actualMode)
        {
            return new HubProtocolReaderWriter(_protocol, GetDataEncoder(requestedMode, actualMode));
        }

        private IDataEncoder GetDataEncoder(TransferMode requestedMode, TransferMode actualMode)
        {
            if (requestedMode == TransferMode.Binary && actualMode == TransferMode.Text)
            {
                // This is for instance for SSE which is a Text protocol and the user wants to use a binary
                // protocol so we need to encode messages.
                return new Base64Encoder();
            }

            Debug.Assert(requestedMode == actualMode, "All transports besides SSE are expected to support binary mode.");

            return new PassThroughEncoder();
        }

        private async Task NegotiateProtocol()
        {
            _logger.LogDebug($"Negotiate to use hub protocol: {_protocol.Name}");
            using (var memoryStream = new MemoryStream())
            {
                NegotiationProtocol.WriteMessage(new NegotiationMessage(_protocol.Name), memoryStream);
                await _connection.SendAsync(memoryStream.ToArray(), CancellationToken.None);
            }
        }

        // TODO: Right now no one is calling this API. We should probably hide it.
        public async Task StopAsync()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            await StopAsyncCore();

            _isConnected = false;

            await RunUserCallbackAsync(DisconnectCallback, () => _options.OnDisconnected?.Invoke(null));
        }

        private Task StopAsyncCore() => _connection.StopAsync();

        private void TimeoutElapsed()
        {
            _connection.AbortAsync(new TimeoutException(
                $"Server timeout ({_options.ServerTimeout.TotalMilliseconds:0.00}ms) elapsed without receiving a message from the server."));
        }

        private void ResetTimeoutTimer()
        {
            if (_needKeepAlive)
            {
                _logger.LogDebug("Resetting keep alive timer...");
                _timeoutTimer.Change(_options.ServerTimeout, Timeout.InfiniteTimeSpan);
            }
        }

        private void ResetReconnectTimer()
        {
            if (_options.AutoReconnect)
            {
                var interval = _reconnectIntervalInMS;
                _logger.LogDebug($"Auto-reconnect is enabled. Will reconnect in {interval} ms.");
                _reconnectTimer.Change(TimeSpan.FromMilliseconds(interval), Timeout.InfiniteTimeSpan);
            }
            else
            {
                _logger.LogDebug("Auto-reconnect is disabled.");
            }
        }

        private void OnHttpConnectionClosed(Exception ex)
        {
            _isConnected = false;

            // Quick return when StopAsync has been called.
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            if (ex != null)
            {
                _logger.LogError($"Connection to Azure SignalR is closed due to error: {ex.Message}");
            }
            else
            {
                _logger.LogWarning("Connection to Azure SignalR is closed.");
            }

            RunUserCallbackAsync(DisconnectCallback, () => _options.OnDisconnected?.Invoke(ex))
                .GetAwaiter().GetResult();

            ResetReconnectTimer();
        }

        private async Task RunUserCallbackAsync(string name, Func<Task> callback)
        {
            try
            {
                if (callback != null)
                {
                    await (callback() ?? Task.CompletedTask);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception when calling {name}: {ex}");
            }
        }

        private async Task OnDataReceivedAsync(byte[] data)
        {
            if (!_isConnected)
            {
                _logger.LogWarning("Message processing is disabled when disconnected.");
                return;
            }

            ResetTimeoutTimer();

            if (_protocolReaderWriter.ReadMessages(data, _hubDispatcher, out var messages))
            {
                foreach (var message in messages)
                {
                    if (message is HubInvocationMessage invocationMessage)
                    {
                        _ = DispatchAsync(invocationMessage);
                    }
                    else
                    {
                        _logger.LogDebug($"Ignore non-HubInvocationMesssage: {message}");
                    }
                }

                await Task.CompletedTask;
            }
        }

        private async Task DispatchAsync(HubInvocationMessage message)
        {
            _logger.LogInformation($"Received mssage: {message}");
            var isMethodInvocation = message is HubMethodInvocationMessage;
            if (isMethodInvocation &&
                OnConnectedAsyncMethod.IgnoreCaseEquals(((HubMethodInvocationMessage) message).Target))
            {
                await OnClientConnectedAsync(message);
                return;
            }

            var connection = GetHubConnectionContext(message);
            if (connection == null)
            {
                await SendMessageAsync(CompletionMessage.WithError(message.InvocationId, "No connection found."));
                return;
            }

            if (isMethodInvocation &&
                OnDisconnectedAsyncMethod.IgnoreCaseEquals(((HubMethodInvocationMessage) message).Target))
            {
                await OnClientDisconnectedAsync(connection);
                return;
            }

            await _hubDispatcher.DispatchMessageAsync(connection, message);
        }

        private async Task OnClientConnectedAsync(HubInvocationMessage message)
        {
            var connection = CreateHubConnectionContext(message);
            _connections.Add(connection);

            await _lifetimeManager.OnConnectedAsync(connection);

            await _hubDispatcher.OnConnectedAsync(connection);
        }

        private async Task OnClientDisconnectedAsync(HubConnectionContext connection)
        {
            await _hubDispatcher.OnDisconnectedAsync(connection, null);

            await _lifetimeManager.OnDisconnectedAsync(connection);

            _connections.Remove(connection);
        }

        private HubConnectionContext CreateHubConnectionContext(HubInvocationMessage message)
        {
            return new CloudHubConnectionContext(_connection, CreateConnectionContext(message), Timeout.InfiniteTimeSpan, _loggerFactory)
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
                connectionContext.User.AddIdentity(new ClaimsIdentity(claims, "Bearer"));
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
            await _connection.SendAsync(payload, CancellationToken.None);
        }

        private class TransferModeFeature : ITransferModeFeature
        {
            public TransferMode TransferMode { get; set; }
        }
    }
}
