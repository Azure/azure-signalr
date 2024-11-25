// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

/// <summary>
/// The client connection context
/// </summary>
/// <code>
///   ------------------------- Client Connection-------------------------------                   ------------Service Connection---------
///  |                                      Transport              Application  |                 |   Transport              Application  |
///  | ========================            =============         ============   |                 |  =============         ============   |
///  | |                      |            |   Input   |         |   Output |   |                 |  |   Input   |         |   Output |   |
///  | |      User's          |  /-------  |     |---------------------|    |   |    /-------     |  |     |---------------------|    |   |
///  | |      Delegated       |  \-------  |     |---------------------|    |   |    \-------     |  |     |---------------------|    |   |
///  | |      Handler         |            |           |         |          |   |                 |  |           |         |          |   |
///  | |                      |            |           |         |          |   |                 |  |           |         |          |   |
///  | |                      |  -------\  |     |---------------------|    |   |    -------\     |  |     |---------------------|    |   |
///  | |                      |  -------/  |     |---------------------|    |   |    -------/     |  |     |---------------------|    |   |
///  | |                      |            |   Output  |         |   Input  |   |                 |  |   Output  |         |   Input  |   |
///  | ========================            ============         ============    |                 |  ============         ============    |
///   --------------------------------------------------------------------------                   ---------------------------------------
/// </code>
internal partial class ClientConnectionContext : ConnectionContext,
                                          IClientConnection,
                                          IConnectionUserFeature,
                                          IConnectionItemsFeature,
                                          IConnectionIdFeature,
                                          IConnectionTransportFeature,
                                          IConnectionHeartbeatFeature,
                                          IHttpContextFeature,
                                          IConnectionStatFeature
{
    private const int WritingState = 1;

    private const int CompletedState = 2;

    private const int IdleState = 0;

    private static readonly PipeOptions DefaultPipeOptions = new PipeOptions(
        readerScheduler: PipeScheduler.ThreadPool,
        useSynchronizationContext: false);

    private readonly TaskCompletionSource<object> _connectionEndTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource<object> _hanshakeCompleteTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly CancellationTokenSource _abortOutgoingCts = new CancellationTokenSource();

    private readonly CancellationTokenSource _connectionClosedCts = new CancellationTokenSource();

    private readonly object _heartbeatLock = new object();

    private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

    private readonly Queue<IMemoryOwner<byte>> _bufferedMessages = new();

    private readonly int _closeTimeOutMilliseconds;

    private readonly bool _isMigrated = false;

    private readonly PauseHandler _pauseHandler = new PauseHandler();

    private int _connectionState = IdleState;

    private List<(Action<object> handler, object state)> _heartbeatHandlers;

    private volatile bool _abortOnClose = true;

    private long _lastMessageReceivedAt;

    private long _receivedBytes;

#if !NETSTANDARD
    public override CancellationToken ConnectionClosed => _connectionClosedCts.Token;
#endif

    public override string ConnectionId { get; set; }

    public string InstanceId { get; }

    public IServiceConnection ServiceConnection { get; set; }

    public string HubProtocol { get; }

    // Send "Abort" to service on close except that Service asks SDK to close
    public bool AbortOnClose
    {
        get => _abortOnClose;
        set => _abortOnClose = value;
    }

    public override IFeatureCollection Features { get; }

    public override IDictionary<object, object> Items { get; set; } = new ConnectionItems(new ConcurrentDictionary<object, object>());

    public override IDuplexPipe Transport { get; set; }

    public IDuplexPipe Application { get; set; }

    public ClaimsPrincipal User { get; set; }

    public Task LifetimeTask => _connectionEndTcs.Task;

    public Task HandshakeResponseTask => _hanshakeCompleteTcs.Task;

    public HttpContext HttpContext { get; set; }

    public DateTime LastMessageReceivedAtUtc => new DateTime(Volatile.Read(ref _lastMessageReceivedAt), DateTimeKind.Utc);

    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

    public long ReceivedBytes => Volatile.Read(ref _receivedBytes);

    public ILogger<ServiceConnection> Logger { get; init; } = NullLogger<ServiceConnection>.Instance;

    private CancellationToken OutgoingAborted => _abortOutgoingCts.Token;

    public ClientConnectionContext(OpenConnectionMessage serviceMessage,
                                   Action<HttpContext> configureContext = null,
                                   PipeOptions transportPipeOptions = null,
                                   PipeOptions appPipeOptions = null,
                                   int closeTimeOutMilliseconds = Constants.DefaultCloseTimeoutMilliseconds)
    {
        ConnectionId = serviceMessage.ConnectionId;
        HubProtocol = serviceMessage.Protocol;
        User = serviceMessage.GetUserPrincipal();
        InstanceId = GetInstanceId(serviceMessage.Headers);

        // Create the Duplix Pipeline for the virtual connection
        transportPipeOptions ??= DefaultPipeOptions;
        appPipeOptions ??= DefaultPipeOptions;

        var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
        Transport = pair.Application;
        Application = pair.Transport;

        HttpContext = BuildHttpContext(serviceMessage);
        configureContext?.Invoke(HttpContext);

        Features = BuildFeatures(serviceMessage);

        if (serviceMessage.Headers.TryGetValue(Constants.AsrsMigrateFrom, out _))
        {
            _isMigrated = true;
            Log.MigrationStarting(Logger, ConnectionId);
        }
        else
        {
            Log.ConnectedStarting(Logger, ConnectionId);
        }

        _closeTimeOutMilliseconds = closeTimeOutMilliseconds;
    }

    private enum ForwardMessageResult
    {
        Success,

        Error,

        Fatal,
    }

    public void CompleteIncoming()
    {
        // always set the connection state to completing when this method is called
        var previousState =
            Interlocked.Exchange(ref _connectionState, CompletedState);

        Application.Output.CancelPendingFlush();

        // If it is idle, complete directly
        // If it is completing already, complete directly
        if (previousState != WritingState)
        {
            Application.Output.Complete();
        }
    }

    public void OnCompleted()
    {
        _connectionEndTcs.TrySetResult(null);
    }

    public void OnHeartbeat(Action<object> action, object state)
    {
        lock (_heartbeatLock)
        {
            _heartbeatHandlers ??= new List<(Action<object> handler, object state)>();
            _heartbeatHandlers.Add((action, state));
        }
    }

    public void TickHeartbeat()
    {
        lock (_heartbeatLock)
        {
            if (_heartbeatHandlers == null)
            {
                return;
            }

            foreach (var (handler, state) in _heartbeatHandlers)
            {
                handler(state);
            }
        }
    }

    /// <summary>
    /// Cancel the outgoing process
    /// </summary>
    public void CancelOutgoing(bool wait = false)
    {
        if (!wait)
        {
            _abortOutgoingCts.Cancel();
        }
        else
        {
            _abortOutgoingCts.CancelAfter(_closeTimeOutMilliseconds);
        }
    }

    public Task PauseAsync()
    {
        Log.OutgoingTaskPaused(Logger, ConnectionId);
        return _pauseHandler.PauseAsync();
    }

    public Task PauseAckAsync()
    {
        if (_pauseHandler.ShouldReplyAck)
        {
            Log.OutgoingTaskPauseAck(Logger, ConnectionId);
            var message = new ConnectionFlowControlMessage(ConnectionId, ConnectionFlowControlOperation.PauseAck);
            var task = ServiceConnection.WriteAsync(message);
            return task;
        }
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        Log.OutgoingTaskResume(Logger, ConnectionId);
        return _pauseHandler.ResumeAsync();
    }

    internal static bool TryGetRemoteIpAddress(IHeaderDictionary headers, out IPAddress address)
    {
        var forwardedFor = headers.GetCommaSeparatedValues("X-Forwarded-For");
        if (forwardedFor.Length > 0 && IPAddress.TryParse(forwardedFor[0], out address))
        {
            return true;
        }
        address = null;
        return false;
    }

    internal async Task ProcessOutgoingMessagesAsync(SignalRProtocol.IHubProtocol protocol)
    {
        try
        {
            while (true)
            {
                var result = await Application.Input.ReadAsync(OutgoingAborted);

                if (result.IsCanceled)
                {
                    break;
                }

                var buffer = result.Buffer;

                if (!buffer.IsEmpty)
                {
                    if (!HandshakeResponseTask.IsCompleted)
                    {
                        var next = buffer;
                        if (SignalRProtocol.HandshakeProtocol.TryParseResponseMessage(ref next, out var message))
                        {
                            if (_isMigrated)
                            {
                                // simply skip the handshake response.
                                buffer = buffer.Slice(next.Start);
                            }
                            else
                            {
                                var dataMessage = new ConnectionDataMessage(ConnectionId, buffer.Slice(0, next.Start))
                                {
                                    Type = DataMessageType.Handshake
                                };
                                var forwardResult = await ForwardMessage(dataMessage);
                                buffer = forwardResult switch
                                {
                                    ForwardMessageResult.Success => buffer.Slice(next.Start),
                                    _ => throw new ForwardMessageException(forwardResult),
                                };
                            }
                            _hanshakeCompleteTcs.TrySetResult(null);
                        }
                    }
                    if (HandshakeResponseTask.IsCompleted)
                    {
                        var next = buffer;
                        while (!buffer.IsEmpty && protocol.TryParseMessage(ref next, FakeInvocationBinder.Instance, out var message))
                        {
                            // we still want messages to successfully going out when application completes
                            if (!await _pauseHandler.WaitAsync(StaticRandom.Next(500, 1500), OutgoingAborted))
                            {
                                Log.OutgoingTaskPaused(Logger, ConnectionId);
                                buffer = buffer.Slice(0);
                                break;
                            }

                            try
                            {
                                var messageType = message switch
                                {
                                    SignalRProtocol.HubInvocationMessage => DataMessageType.Invocation,
                                    SignalRProtocol.CloseMessage => DataMessageType.Close,
                                    _ => DataMessageType.Other,
                                };
                                var dataMessage = new ConnectionDataMessage(ConnectionId, buffer.Slice(0, next.Start))
                                {
                                    Type = messageType
                                };
                                var forwardResult = await ForwardMessage(dataMessage);
                                buffer = forwardResult switch
                                {
                                    ForwardMessageResult.Fatal => throw new ForwardMessageException(forwardResult),
                                    _ => next,
                                };
                            }
                            finally
                            {
                                _pauseHandler.Release();
                            }
                        }
                    }
                }

                if (result.IsCompleted)
                {
                    // This connection ended (the application itself shut down) we should remove it from the list of connections
                    break;
                }

                Application.Input.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        catch (OperationCanceledException)
        {
            // cancelled
        }
        catch (ForwardMessageException)
        {
            // do nothing.
        }
        catch (Exception ex)
        {
            // The exception means application fail to process input anymore
            // Cancel any pending flush so that we can quit and perform disconnect
            // Here is abort close and WaitOnApplicationTask will send close message to notify client to disconnect
            Log.SendLoopStopped(Logger, ConnectionId, ex);
            Application.Output.CancelPendingFlush();
        }
        finally
        {
            Application.Input.Complete();
        }
    }

    internal async Task ProcessApplicationAsync(ConnectionDelegate connectionDelegate)
    {
        Exception exception = null;

        try
        {
            // Wait for the application task to complete
            // application task can end when exception, or Context.Abort() from hub
            await connectionDelegate(this);
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
            Transport.Output.Complete(exception);
            Transport.Input.Complete();
        }
    }

    internal async Task PerformDisconnectAsync()
    {
        ClearBufferedMessages();

        // In normal close, service already knows the client is closed, no need to be informed.
        AbortOnClose = false;

        // We're done writing to the application output
        // Let the connection complete incoming
        CompleteIncoming();

        // Wait for the connection's lifetime task to end
        // Wait on the application task to complete
        // We wait gracefully here to be consistent with self-host SignalR
        using var cts = new CancellationTokenSource(_closeTimeOutMilliseconds);
        try
        {
            await LifetimeTask.OrCancelAsync(cts.Token);
            cts.Cancel();
        }
        catch (OperationCanceledException)
        {

            Log.DetectedLongRunningApplicationTask(Logger, ConnectionId, _closeTimeOutMilliseconds);
#if !NETSTANDARD
            _connectionClosedCts.Cancel();
#endif
        }
    }

    internal async Task ProcessConnectionDataMessageAsync(ConnectionDataMessage connectionDataMessage)
    {
        try
        {
#if !NET8_0_OR_GREATER
            // do NOT write close message until net 8 or later.
            if (connectionDataMessage.Type == DataMessageType.Close)
            {
                return;
            }
#endif
            if (connectionDataMessage.IsPartial)
            {
                var owner = ExactSizeMemoryPool.Shared.Rent((int)connectionDataMessage.Payload.Length);
                connectionDataMessage.Payload.CopyTo(owner.Memory.Span);
                // make sure there is no await operation before _bufferingMessages.
                _bufferedMessages.Enqueue(owner);
            }
            else
            {
                long length = 0;
                foreach (var owner in _bufferedMessages)
                {
                    using (owner)
                    {
                        await WriteToApplicationAsync(new ReadOnlySequence<byte>(owner.Memory));
                        length += owner.Memory.Length;
                    }
                }
                _bufferedMessages.Clear();

                var payload = connectionDataMessage.Payload;
                length += payload.Length;
                Log.WriteMessageToApplication(Logger, length, connectionDataMessage.ConnectionId);
                await WriteToApplicationAsync(payload);
            }
        }
        catch (Exception ex)
        {
            Log.FailToWriteMessageToApplication(Logger, connectionDataMessage, ex);
        }
    }

    internal void ClearBufferedMessages()
    {
        _bufferedMessages.Clear();
    }

    private static void ProcessQuery(string queryString, out string originalPath)
    {
        originalPath = string.Empty;
        var query = QueryHelpers.ParseNullableQuery(queryString);
        if (query == null)
        {
            return;
        }

        if (query.TryGetValue(Constants.QueryParameter.RequestCulture, out var culture))
        {
            SetCurrentThreadCulture(culture.FirstOrDefault());
        }
        if (query.TryGetValue(Constants.QueryParameter.RequestUICulture, out var uiCulture))
        {
            SetCurrentThreadUiCulture(uiCulture.FirstOrDefault());
        }
        if (query.TryGetValue(Constants.QueryParameter.OriginalPath, out var path))
        {
            originalPath = path.FirstOrDefault();
        }
    }

    private static void SetCurrentThreadCulture(string cultureName)
    {
        if (!string.IsNullOrEmpty(cultureName))
        {
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            }
            catch (Exception)
            {
                // skip invalid culture, normal won't hit.
            }
        }
    }

    private static void SetCurrentThreadUiCulture(string uiCultureName)
    {
        if (!string.IsNullOrEmpty(uiCultureName))
        {
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(uiCultureName);
            }
            catch (Exception)
            {
                // skip invalid culture, normal won't hit.
            }
        }
    }

    private static string GetInstanceId(IDictionary<string, StringValues> header)
    {
        return header.TryGetValue(Constants.AsrsInstanceId, out var instanceId) ? (string)instanceId : string.Empty;
    }

    /// <summary>
    /// Forward message to service
    /// </summary>
    private async Task<ForwardMessageResult> ForwardMessage(ConnectionDataMessage data)
    {
        try
        {
            // Forward the message to the service
            await ServiceConnection.WriteAsync(data);
            return ForwardMessageResult.Success;
        }
        catch (ServiceConnectionNotActiveException)
        {
            // Service connection not active means the transport layer for this connection is closed, no need to continue processing
            return ForwardMessageResult.Fatal;
        }
        catch (Exception ex)
        {
            Log.ErrorSendingMessage(Logger, ex);
            return ForwardMessageResult.Error;
        }
    }

    private async Task WriteToApplicationAsync(ReadOnlySequence<byte> payload)
    {
        await _writeLock.WaitAsync();
        try
        {
            var previousState = Interlocked.CompareExchange(ref _connectionState, WritingState, IdleState);

            // Write should not be called from multiple threads
            Debug.Assert(previousState != WritingState);

            if (previousState == CompletedState)
            {
                // already completing, don't write anymore
                return;
            }

            try
            {
                _lastMessageReceivedAt = DateTime.UtcNow.Ticks;
                _receivedBytes += payload.Length;

                // Start write
                await WriteMessageAsyncCore(payload);
            }
            finally
            {
                // Try to set the connection to idle if it is in writing state, if it is in complete state, complete the tcs
                previousState = Interlocked.CompareExchange(ref _connectionState, IdleState, WritingState);
                if (previousState == CompletedState)
                {
                    Application.Output.Complete();
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private FeatureCollection BuildFeatures(OpenConnectionMessage serviceMessage)
    {
        var features = new FeatureCollection();
        features.Set<IConnectionHeartbeatFeature>(this);
        features.Set<IConnectionUserFeature>(this);
        features.Set<IConnectionItemsFeature>(this);
        features.Set<IConnectionIdFeature>(this);
        features.Set<IConnectionTransportFeature>(this);
        features.Set<IHttpContextFeature>(this);
        features.Set<IConnectionStatFeature>(this);

        var userIdClaim = serviceMessage.Claims?.FirstOrDefault(c => c.Type == Constants.ClaimType.UserId);
        if (userIdClaim != default)
        {
            features.Set(new ServiceUserIdFeature(userIdClaim.Value));
        }
        return features;
    }

    private async Task WriteMessageAsyncCore(ReadOnlySequence<byte> payload)
    {
        if (payload.IsSingleSegment)
        {
            // Write the raw connection payload to the pipe let the upstream handle it
            await Application.Output.WriteAsync(payload.First);
        }
        else
        {
            var position = payload.Start;
            while (payload.TryGet(ref position, out var memory))
            {
                var result = await Application.Output.WriteAsync(memory);
                if (result.IsCanceled)
                {
                    // IsCanceled when CancelPendingFlush is called
                    break;
                }
            }
        }
    }

    private DefaultHttpContext BuildHttpContext(OpenConnectionMessage message)
    {
        var httpContextFeatures = new FeatureCollection();
        ProcessQuery(message.QueryString, out var originalPath);
        var requestFeature = new HttpRequestFeature
        {
            Headers = new HeaderDictionary((Dictionary<string, StringValues>)message.Headers),
            QueryString = message.QueryString,
            Path = originalPath
        };

        httpContextFeatures.Set<IHttpRequestFeature>(requestFeature);
        httpContextFeatures.Set<IHttpAuthenticationFeature>(new HttpAuthenticationFeature
        {
            User = User
        });

        if (TryGetRemoteIpAddress(requestFeature.Headers, out var address))
        {
            httpContextFeatures.Set<IHttpConnectionFeature>(new HttpConnectionFeature { RemoteIpAddress = address });
        }

        return new DefaultHttpContext(httpContextFeatures);
    }

    private sealed class FakeInvocationBinder : IInvocationBinder
    {
        public static readonly FakeInvocationBinder Instance = new FakeInvocationBinder();

        public IReadOnlyList<Type> GetParameterTypes(string methodName) => Type.EmptyTypes;

        public Type GetReturnType(string invocationId) => typeof(object);

        public Type GetStreamItemType(string streamId) => typeof(object);
    }

    private sealed class ForwardMessageException : Exception
    {
        public ForwardMessageResult Result { get; }

        public ForwardMessageException(ForwardMessageResult result)
        {
            Result = result;
        }
    }
}
