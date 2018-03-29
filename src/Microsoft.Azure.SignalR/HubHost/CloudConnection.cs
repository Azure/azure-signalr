// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Formatters;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Microsoft.AspNetCore.Sockets.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    public class CloudConnection<THub> : IMessageSender where THub : Hub
    {
        private const int MaxReconnectInterval = 1000; // in milliseconds
        private const string ConnectCallback = "HubHostOptions.OnConnected";
        private const string DisconnectCallback = "HubHostOptions.OnDisconnected";

        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IConnection _connection;
        private readonly HubHostOptions _options;
        private readonly IHubProtocol _protocol;

        private readonly IHubProtocolResolver _protocolResolver;
        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly HubDispatcher<THub> _hubDispatcher;
        private readonly IHubProtocol _jsonProtocol;
        private readonly IHubProtocol _messagePackProtocol;
        private readonly HubConnectionStore _connections = new HubConnectionStore();

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // TODO: expose connection status to user code
        private readonly Timer _timeoutTimer;
        private bool _needKeepAlive;

        private CancellationTokenSource _connectionActive;
        private readonly Timer _reconnectTimer;
        private int _reconnectIntervalInMS => StaticRandom.Next(MaxReconnectInterval);
        private bool _receivedHandshakeResponse;

        public CloudConnection(IConnection connection,
            IHubProtocol protocol,
            HubHostOptions options,
            HubLifetimeManager<THub> lifetimeManager,
            HubDispatcher<THub> hubDispatcher,
            ILoggerFactory loggerFactory,
            IHubProtocolResolver hubProtocolResolver,
            IHubProtocol jsonProtocol,
            IHubProtocol messagePackProtocol)
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

            _protocolResolver = hubProtocolResolver;
            _jsonProtocol = jsonProtocol;
            _messagePackProtocol = messagePackProtocol;

            connection.OnReceived((data, state) => ((CloudConnection<THub>) state).OnDataReceivedAsync(data), this);
            connection.Closed += OnHttpConnectionClosed;
        }

        public async Task StartAsync()
        {
            try
            {
                await StartAsyncCore();
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
            await _connection.StartAsync(_protocol.TransferFormat);
            _needKeepAlive = _connection.Features.Get<IConnectionInherentKeepAliveFeature>() == null;
            _receivedHandshakeResponse = false;

            //Log.HubProtocol(_logger, _protocol.Name, _protocol.Version);

            _connectionActive = new CancellationTokenSource();
            using (var memoryStream = new MemoryStream())
            {
                //Log.SendingHubHandshake(_logger);
                HandshakeProtocol.WriteRequestMessage(new HandshakeRequestMessage(_protocol.Name, _protocol.Version), memoryStream);
                await _connection.SendAsync(memoryStream.ToArray(), _connectionActive.Token);
            }
            ResetTimeoutTimer();
        }

        // TODO: Right now no one is calling this API. We should probably hide it.
        public async Task StopAsync()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            await StopAsyncCore();

            await RunUserCallbackAsync(DisconnectCallback, () => _options.OnDisconnected?.Invoke(null));
        }

        public Task SendAllProtocolRawMessage(IDictionary<string, string> meta, string method, object[] args)
        {
            var hubInvocationMessageWrapper = new HubInvocationMessageWrapper(_protocol.TransferFormat);
            hubInvocationMessageWrapper.AddMetadata(meta);
            if (method != null)
            {
                var message = CreateInvocationMessage(method, args);
                hubInvocationMessageWrapper.JsonPayload = _jsonProtocol.WriteToArray(message);
                hubInvocationMessageWrapper.MsgpackPayload = _messagePackProtocol.WriteToArray(message);
            }
            _ = SendHubMessage(hubInvocationMessageWrapper);
            return Task.CompletedTask;
        }

        public Task SendHubMessage(HubMessage message)
        {
            return SendMessageAsync(message);
        }

        public InvocationMessage CreateInvocationMessage(string methodName, object[] args)
        {
            var invocationMessage = new InvocationMessage(
                target: methodName,
                argumentBindingException: null, arguments: args);
            return invocationMessage;
        }

        private Task StopAsyncCore() => _connection.StopAsync();

        private void TimeoutElapsed()
        {
            _connection.AbortAsync(new TimeoutException(
                $"Server timeout ({TimeSpan.FromSeconds(_options.ServerTimeout).TotalMilliseconds:0.00}ms) elapsed without receiving a message from the server."));
        }

        private void ResetTimeoutTimer()
        {
            if (_needKeepAlive)
            {
                _logger.LogDebug("Resetting keep alive timer...");
                _timeoutTimer.Change(TimeSpan.FromSeconds(_options.ServerTimeout), Timeout.InfiniteTimeSpan);
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

        private bool ProcessHandshakeResponse(ref ReadOnlyMemory<byte> data)
        {
            HandshakeResponseMessage message;

            try
            {
                // read first message out of the incoming data
                if (!TextMessageParser.TryParseMessage(ref data, out var payload))
                {
                    throw new InvalidDataException("Unable to parse payload as a handshake response message.");
                }

                message = HandshakeProtocol.ParseResponseMessage(payload);
            }
            catch (Exception)
            {
                // shutdown if we're unable to read handshake
                //Log.ErrorReceivingHandshakeResponse(_logger, ex);
                //Shutdown(ex);
                return false;
            }

            if (!string.IsNullOrEmpty(message.Error))
            {
                // shutdown if handshake returns an error
                //Log.HandshakeServerError(_logger, message.Error);
                //Shutdown();
                return false;
            }

            return true;
        }

        private async Task OnDataReceivedAsync(byte[] data)
        {
            ResetTimeoutTimer();

            var currentData = new ReadOnlyMemory<byte>(data);
            //Log.ParsingMessages(_logger, currentData.Length);

            // first message received must be handshake response
            if (!_receivedHandshakeResponse)
            {
                // process handshake and return left over data to parse additional messages
                if (!ProcessHandshakeResponse(ref currentData))
                {
                    return;
                }

                _receivedHandshakeResponse = true;
                if (currentData.IsEmpty)
                {
                    return;
                }
            }

            var messages = new List<HubMessage>();
            if (_protocol.TryParseMessages(currentData, _hubDispatcher, messages))
            {
                foreach (var message in messages)
                {
                    switch (message)
                    {
                        case HubInvocationMessageWrapper hubInvocationMessageWrapper:
                            _ = DispatchAsync(hubInvocationMessageWrapper);
                            break;
                        case PingMessage _:
                            break;
                        default:
                            //Logger.UnsupportedMessageReceived(message.GetType().FullName);
                            throw new NotSupportedException($"Received unsupported message: {message}");
                    }
                }

                await Task.CompletedTask;
            }
        }

        private async Task DispatchAsync(HubInvocationMessageWrapper hubMessage)
        {
            switch (hubMessage.InvocationType)
            {
                case HubInvocationType.OnConnected:
                    await OnConnectedAsync(hubMessage);
                    break;
                case HubInvocationType.OnDisconnected:
                    await OnDisconnectedAsync(hubMessage);
                    break;
                default:
                    var connection = GetHubConnectionContext(hubMessage);
                    if (connection == null)
                    {
                        _logger.LogError("Fail to get Hub connection context information");
                        return;
                    }
                    await _hubDispatcher.DispatchMessageAsync(connection, hubMessage);
                    break;
            }
        }

        private async Task OnConnectedAsync(HubInvocationMessageWrapper message)
        {
            var connection = CreateHubConnectionContext(message);
            _connections.Add(connection);

            await _lifetimeManager.OnConnectedAsync(connection);

            await _hubDispatcher.OnConnectedAsync(connection);
        }

        private async Task OnDisconnectedAsync(HubInvocationMessageWrapper message)
        {
            var connection = GetHubConnectionContext(message);
            if (connection == null)
            {
                _logger.LogError("Fail to get Hub connection context information");
                return;
            }
            await _hubDispatcher.OnDisconnectedAsync(connection, null);

            await _lifetimeManager.OnDisconnectedAsync(connection);

            _connections.Remove(connection);
        }

        private HubConnectionContext CreateHubConnectionContext(HubInvocationMessageWrapper message)
        {
            var context = CreateConnectionContext(message);

            var protocolName = message.Format == TransferFormat.Binary ? MessagePackHubProtocol.ProtocolName : JsonHubProtocol.ProtocolName;
            return new CloudHubConnectionContext(_protocolResolver.GetProtocol(protocolName, null, null), this, context, _loggerFactory);
        }

        private DefaultConnectionContext CreateConnectionContext(HubInvocationMessageWrapper message)
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

        private HubConnectionContext GetHubConnectionContext(HubInvocationMessageWrapper message)
        {
            return message.TryGetConnectionId(out var connectionId) ? _connections[connectionId] : null;
        }

        private async Task SendMessageAsync(HubMessage hubMessage)
        {
            // TODO. When streaming is supported, here needs a lock here for race contention.
            // This logic should be re-implemented after SignalR core will fix the issue https://github.com/aspnet/SignalR/pull/1718
            var payload = _protocol.WriteToArray(hubMessage);
            await _connection.SendAsync(payload, CancellationToken.None);
        }
    }
}
