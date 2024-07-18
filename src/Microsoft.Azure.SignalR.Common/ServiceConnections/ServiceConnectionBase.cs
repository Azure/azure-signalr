// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

internal abstract partial class ServiceConnectionBase : IServiceConnection
{
    protected static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(15);

    // Service ping rate is 5 sec to let server know service status. Set timeout for 30 sec for some space.
    private static readonly TimeSpan DefaultServiceTimeout = TimeSpan.FromSeconds(30);

    private static readonly long DefaultServiceTimeoutTicks = (long)(DefaultServiceTimeout.TotalSeconds * Stopwatch.Frequency);

    // App server ping rate is 5 sec to let service know if app server is still alive
    // Service will abort both server and client connections link to this server when server is down.
    // App server ping is triggered by incoming requests and send by checking last send timestamp.
    private static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(5);

    // App server update its azure identity by sending a AccessKeyRequestMessage with Azure AD Token every 10 minutes.
    private static readonly TimeSpan DefaultSyncAzureIdentityInterval = TimeSpan.FromMinutes(10);

    private static readonly long DefaultKeepAliveTicks = (long)DefaultKeepAliveInterval.TotalSeconds * Stopwatch.Frequency;

    private readonly ReadOnlyMemory<byte> _cachedPingBytes;

    private readonly HandshakeRequestMessage _handshakeRequest;

    private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

    private readonly TaskCompletionSource<bool> _serviceConnectionStartTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<object> _serviceConnectionOfflineTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly ServiceConnectionType _connectionType;

    private readonly IServiceMessageHandler _serviceMessageHandler;

    private readonly IServiceEventHandler _serviceEventHandler;

    private readonly object _statusLock = new object();

    private readonly string _endpointName;

    private volatile string _errorMessage;

    // Check service timeout
    private long _lastReceiveTimestamp;

    // Keep-alive tick
    private long _lastSendTimestamp;

    private ServiceConnectionStatus _status;

    private int _started;

    private ConnectionContext _connectionContext;

    public ServiceConnectionStatus Status
    {
        get => _status;

        protected set
        {
            if (_status != value)
            {
                lock (_statusLock)
                {
                    if (_status != value)
                    {
                        var prev = _status;
                        _status = value;
                        ConnectionStatusChanged?.Invoke(new StatusChange(prev, value));
                    }
                }
            }
        }
    }

    public Task ConnectionInitializedTask => _serviceConnectionStartTcs.Task;

    public Task ConnectionOfflineTask => _serviceConnectionOfflineTcs.Task;

    protected HubServiceEndpoint HubEndpoint { get; }

    protected string ServerId { get; }

    protected string ConnectionId { get; }

    protected ILogger Logger { get; }

    protected IServiceProtocol ServiceProtocol { get; }

    protected ServiceConnectionBase(
        IServiceProtocol serviceProtocol,
        string serverId,
        string connectionId,
        HubServiceEndpoint endpoint,
        IServiceMessageHandler serviceMessageHandler,
        IServiceEventHandler serviceEventHandler,
        ServiceConnectionType connectionType,
        ILogger logger,
        GracefulShutdownMode mode = GracefulShutdownMode.Off,
        bool allowStatefulReconnects = false)
    {
        ServiceProtocol = serviceProtocol;
        ServerId = serverId;
        ConnectionId = connectionId;

        _connectionType = connectionType;
        HubEndpoint = endpoint;
        _endpointName = HubEndpoint?.ToString() ?? string.Empty;
        if (serviceProtocol != null)
        {
            _cachedPingBytes = serviceProtocol.GetMessageBytes(PingMessage.Instance);

            var migrationLevel = mode == GracefulShutdownMode.MigrateClients ? 1 : 0;
            _handshakeRequest = new HandshakeRequestMessage(serviceProtocol.Version, (int)connectionType, migrationLevel) { AllowStatefulReconnects = allowStatefulReconnects };
        }

        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceMessageHandler = serviceMessageHandler;
        _serviceEventHandler = serviceEventHandler;
    }

    public event Action<StatusChange> ConnectionStatusChanged;

    /// <summary>
    /// Start a service connection without the lifetime management.
    /// To get full lifetime management including dispose or restart, use <see cref="ServiceConnectionContainerBase"/>
    /// </summary>
    /// <param name="target">The target instance Id</param>
    /// <returns>The task of StartAsync</returns>
    public async Task StartAsync(string target = null)
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            throw new InvalidOperationException("Connection already started!");
        }

        Status = ServiceConnectionStatus.Connecting;

        var connection = await EstablishConnectionAsync(target);
        if (connection != null)
        {
            _connectionContext = connection;
            Status = ServiceConnectionStatus.Connected;
            _serviceConnectionStartTcs.TrySetResult(true);
            try
            {
                TimerAwaitable syncTimer = null;
                try
                {
                    if (HubEndpoint != null && HubEndpoint.AccessKey is AccessKeyForMicrosoftEntra key)
                    {
                        syncTimer = new TimerAwaitable(TimeSpan.Zero, DefaultSyncAzureIdentityInterval);
                        _ = UpdateAzureIdentityAsync(key, syncTimer);
                    }
                    await ProcessIncomingAsync(connection);
                }
                finally
                {
                    syncTimer?.Stop();

                    // when ProcessIncoming completes, clean up the connection

                    // TODO: Never cleanup connections unless Service asks us to do that
                    // Current implementation is based on assumption that Service will drop clients
                    // if server connection fails.
                    await CleanupClientConnections();
                }
            }
            catch (Exception ex)
            {
                Log.ConnectionDropped(Logger, _endpointName, ConnectionId, ex);
            }
            finally
            {
                // wait until all the connections are cleaned up to close the outgoing pipe
                // mark the status as Disconnected so that no one will write to this connection anymore
                // Don't allow write anymore when the connection is disconnected
                Status = ServiceConnectionStatus.Disconnected;

                await _writeLock.WaitAsync();
                try
                {
                    // close the underlying connection
                    await DisposeConnection(connection);
                }
                finally
                {
                    _writeLock.Release();
                }
            }
        }
        else
        {
            Status = ServiceConnectionStatus.Disconnected;
            _serviceConnectionStartTcs.TrySetResult(false);
        }
    }

    public Task StopAsync()
    {
        try
        {
            _connectionContext?.Transport.Input.CancelPendingRead();
        }
        catch (Exception ex)
        {
            Log.UnexpectedExceptionInStop(Logger, ConnectionId, ex);
        }
        return Task.CompletedTask;
    }

    public async Task WriteAsync(ServiceMessage serviceMessage)
    {
        if (!await SafeWriteAsync(serviceMessage).ConfigureAwait(false))
        {
            throw new ServiceConnectionNotActiveException(_errorMessage);
        }
    }

    protected virtual async Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
    {
        if (!string.IsNullOrEmpty(_errorMessage) || Status != ServiceConnectionStatus.Connected)
        {
            return false;
        }

        await _writeLock.WaitAsync().ConfigureAwait(false);

        if (Status != ServiceConnectionStatus.Connected)
        {
            // Make sure not write messages to the connection when it is no longer connected
            _writeLock.Release();
            return false;
        }
        try
        {
            // Write the service protocol message
            ServiceProtocol.WriteMessage(serviceMessage, _connectionContext.Transport.Output);
            await _connectionContext.Transport.Output.FlushAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            // We always mark the connection as Disconnected before dispose the underlying http connection
            // So in theory this log should never trigger
            Log.FailedToWrite(Logger, (serviceMessage as IMessageWithTracingId)?.TracingId, ConnectionId, ex);
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    protected abstract Task<ConnectionContext> CreateConnection(string target = null);

    protected abstract Task DisposeConnection(ConnectionContext connection);

    protected abstract Task CleanupClientConnections(string fromInstanceId = null);

    protected abstract Task OnClientConnectedAsync(OpenConnectionMessage openConnectionMessage);

    protected abstract Task OnClientDisconnectedAsync(CloseConnectionMessage closeConnectionMessage);

    protected abstract Task OnClientMessageAsync(ConnectionDataMessage connectionDataMessage);

    protected Task OnServiceErrorAsync(ServiceErrorMessage serviceErrorMessage)
    {
        if (!string.IsNullOrEmpty(serviceErrorMessage.ErrorMessage))
        {
            // When receives service error message, we suppose server -> service connection doesn't work,
            // and set _errorMessage to prevent sending message from server to service
            // But messages in the pipe from service -> server should be processed as usual. Just log without
            // throw exception here.
            _errorMessage = serviceErrorMessage.ErrorMessage;

            // Update the status immediately
            Status = ServiceConnectionStatus.Disconnected;
            Log.ReceivedServiceErrorMessage(Logger, ConnectionId, serviceErrorMessage.ErrorMessage);
        }

        return Task.CompletedTask;
    }

    protected virtual Task OnPingMessageAsync(PingMessage pingMessage)
    {
        if (RuntimeServicePingMessage.IsEchoMessage(pingMessage))
        {
            return WriteAsync(pingMessage);
        }
        if (RuntimeServicePingMessage.TryGetOffline(pingMessage, out var instanceId))
        {
            Log.ReceivedInstanceOfflinePing(Logger, instanceId);
            return CleanupClientConnections(instanceId);
        }
        if (RuntimeServicePingMessage.IsFinAck(pingMessage))
        {
            _serviceConnectionOfflineTcs.TrySetResult(null);
            return Task.CompletedTask;
        }
        return _serviceMessageHandler.HandlePingAsync(pingMessage);
    }

    protected Task OnAckMessageAsync(AckMessage ackMessage)
    {
        _serviceMessageHandler.HandleAck(ackMessage);
        return Task.CompletedTask;
    }

    protected virtual async Task<bool> HandshakeAsync(ConnectionContext context)
    {
        await SendHandshakeRequestAsync(context.Transport.Output);

        using var cts = new CancellationTokenSource();
        if (!Debugger.IsAttached)
        {
            cts.CancelAfter(DefaultHandshakeTimeout);
        }

        if (await ReceiveHandshakeResponseAsync(context.Transport.Input, cts.Token))
        {
            Log.HandshakeComplete(Logger);
            return true;
        }
        return false;
    }

    protected virtual Task DispatchMessageAsync(ServiceMessage message)
    {
        return message switch
        {
            OpenConnectionMessage openConnectionMessage => OnClientConnectedAsync(openConnectionMessage),
            CloseConnectionMessage closeConnectionMessage => OnClientDisconnectedAsync(closeConnectionMessage),
            ConnectionDataMessage connectionDataMessage => OnClientMessageAsync(connectionDataMessage),
            ServiceErrorMessage serviceErrorMessage => OnServiceErrorAsync(serviceErrorMessage),
            PingMessage pingMessage => OnPingMessageAsync(pingMessage),
            AckMessage ackMessage => OnAckMessageAsync(ackMessage),
            ServiceEventMessage eventMessage => OnEventMessageAsync(eventMessage),
            AccessKeyResponseMessage keyMessage => OnAccessKeyMessageAsync(keyMessage),
            _ => Task.CompletedTask,
        };
    }

    protected virtual async ValueTask TrySendPingAsync()
    {
        if (!_writeLock.Wait(0))
        {
            // Skip sending PingMessage when failed getting lock
            return;
        }

        try
        {
            // Check if last send time is longer than default keep-alive ticks and then send ping
            if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSendTimestamp) > DefaultKeepAliveTicks)
            {
                await _connectionContext.Transport.Output.WriteAsync(GetPingMessage());
                Interlocked.Exchange(ref _lastSendTimestamp, Stopwatch.GetTimestamp());
                Log.SentPing(Logger);
            }
        }
        catch (Exception ex)
        {
            Log.FailedSendingPing(Logger, _endpointName, ConnectionId, ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    protected virtual ReadOnlyMemory<byte> GetPingMessage() => _cachedPingBytes;

    private Task OnEventMessageAsync(ServiceEventMessage message)
    {
        _ = _serviceEventHandler?.HandleAsync(ConnectionId, message);
        return Task.CompletedTask;
    }

    private Task OnAccessKeyMessageAsync(AccessKeyResponseMessage keyMessage)
    {
        if (HubEndpoint.AccessKey is AccessKeyForMicrosoftEntra key)
        {
            if (string.IsNullOrEmpty(keyMessage.ErrorType))
            {
                key.UpdateAccessKey(keyMessage.Kid, keyMessage.AccessKey);
            }
            else if (key.HasExpired)
            {
                Log.AuthorizeFailed(Logger, _endpointName, keyMessage.ErrorMessage, null);
                return Task.CompletedTask;
            }
        }
        return Task.CompletedTask;
    }

    private async Task<ConnectionContext> EstablishConnectionAsync(string target)
    {
        try
        {
            var connectionContext = await CreateConnection(target);
            try
            {
                if (await HandshakeAsync(connectionContext))
                {
                    Log.ServiceConnectionConnected(Logger, ConnectionId);
                    return connectionContext;
                }
            }
            catch (Exception ex)
            {
                Log.HandshakeError(Logger, _endpointName, ex.Message, ConnectionId);
                await DisposeConnection(connectionContext);
                return null;
            }

            // handshake return false
            await DisposeConnection(connectionContext);

            return null;
        }
        catch (Exception ex)
        {
            if (target == null)
            {
                // Log for required connections only to reduce noise for rebalance
                // connection failure usually due to service maintenance.
                Log.FailedToConnect(Logger, _endpointName, ConnectionId, ex);
            }
            return null;
        }
    }

    private async Task SendHandshakeRequestAsync(PipeWriter output)
    {
        Log.SendingHandshakeRequest(Logger);

        ServiceProtocol.WriteMessage(_handshakeRequest, output);
        var sendHandshakeResult = await output.FlushAsync();
        if (sendHandshakeResult.IsCompleted)
        {
            throw new InvalidOperationException("Service disconnected before handshake complete.");
        }
    }

    private async Task<bool> ReceiveHandshakeResponseAsync(PipeReader input, CancellationToken token)
    {
        while (true)
        {
            var result = await input.ReadAsync(token);

            var buffer = result.Buffer;
            var consumed = buffer.Start;
            var examined = buffer.End;

            try
            {
                if (result.IsCanceled)
                {
                    throw new InvalidOperationException("Connection cancelled before handshake complete.");
                }

                if (!buffer.IsEmpty)
                {
                    if (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                    {
                        consumed = buffer.Start;
                        examined = consumed;

                        if (message is not HandshakeResponseMessage handshakeResponse)
                        {
                            throw new InvalidDataException(
                                $"{message.GetType().Name} received when waiting for handshake response.");
                        }

                        if (string.IsNullOrEmpty(handshakeResponse.ErrorMessage))
                        {
                            return true;
                        }

                        // Handshake error. Will stop reconnect.
                        if (_connectionType == ServiceConnectionType.OnDemand)
                        {
                            // Handshake errors on on-demand connections are acceptable.
                            Log.OnDemandConnectionHandshakeResponse(Logger, handshakeResponse.ErrorMessage);
                        }
                        else
                        {
                            Log.HandshakeError(Logger, _endpointName, handshakeResponse.ErrorMessage, ConnectionId);
                        }
                        return false;
                    }
                }

                if (result.IsCompleted)
                {
                    // Not enough data, and we won't be getting any more data.
                    throw new InvalidOperationException("Service disconnected before sending a handshake response.");
                }
            }
            finally
            {
                input.AdvanceTo(consumed, examined);
            }
        }
    }

    private async Task UpdateAzureIdentityAsync(AccessKeyForMicrosoftEntra key, TimerAwaitable timer)
    {
        using (timer)
        {
            timer.Start();
            while (await timer)
            {
                await SendAccessKeyRequestMessageAsync(key);
            }
        }
    }

    private async Task SendAccessKeyRequestMessageAsync(AccessKeyForMicrosoftEntra key)
    {
        try
        {
            var source = new CancellationTokenSource(AccessKeyForMicrosoftEntra.GetAccessKeyTimeout);
            var token = await key.GetMicrosoftEntraTokenAsync(source.Token);
            var message = new AccessKeyRequestMessage(token);
            await SafeWriteAsync(message);
        }
        catch (Exception e)
        {
            Log.SendAccessKeyRequestFailed(Logger, _endpointName, e.Message, e);
        }
    }

    private async Task ProcessIncomingAsync(ConnectionContext connection)
    {
        var keepAliveTimer = StartKeepAliveTimer();

        try
        {
            while (true)
            {
                var result = await connection.Transport.Input.ReadAsync();
                var buffer = result.Buffer;

                try
                {
                    if (result.IsCanceled)
                    {
                        Log.ReadingCancelled(Logger, ConnectionId);
                        break;
                    }

                    if (!buffer.IsEmpty)
                    {
                        Log.ReceivedMessage(Logger, buffer.Length, ConnectionId);

                        UpdateReceiveTimestamp();

                        // No matter what kind of message come in, trigger send ping check
                        _ = TrySendPingAsync();

                        while (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                        {
                            _ = DispatchMessageAsync(message);
                        }
                    }

                    if (result.IsCompleted)
                    {
                        // The connection is closed (reconnect)
                        Log.ServiceConnectionClosed(Logger, ConnectionId);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // Error occurs in handling the message, but the connection between SDK and service still works.
                    // So, just log error instead of breaking the connection
                    Log.ErrorProcessingMessages(Logger, _endpointName, ConnectionId, ex);
                }
                finally
                {
                    connection.Transport.Input.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }
        finally
        {
            keepAliveTimer.Stop();
            _serviceConnectionOfflineTcs.TrySetResult(true);
        }
    }

    private TimerAwaitable StartKeepAliveTimer()
    {
        Log.StartingKeepAliveTimer(Logger, DefaultKeepAliveInterval);

        _lastReceiveTimestamp = Stopwatch.GetTimestamp();
        _lastSendTimestamp = _lastReceiveTimestamp;
        var timer = new TimerAwaitable(DefaultKeepAliveInterval, DefaultKeepAliveInterval);
        _ = KeepAliveAsync(timer);

        return timer;
    }

    private void UpdateReceiveTimestamp()
    {
        Interlocked.Exchange(ref _lastReceiveTimestamp, Stopwatch.GetTimestamp());
    }

    private async Task KeepAliveAsync(TimerAwaitable timer)
    {
        using (timer)
        {
            timer.Start();

            while (await timer)
            {
                if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastReceiveTimestamp) > DefaultServiceTimeoutTicks)
                {
                    Log.ServiceTimeout(Logger, _endpointName, DefaultServiceTimeout, ConnectionId);
                    await StopAsync();

                    // We shouldn't get here twice.
                    continue;
                }
            }
        }
    }
}
