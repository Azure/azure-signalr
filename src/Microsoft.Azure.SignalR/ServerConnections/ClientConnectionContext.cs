﻿// Copyright (c) Microsoft. All rights reserved.
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
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
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
    internal class ClientConnectionContext : ConnectionContext,
                                              IConnectionUserFeature,
                                              IConnectionItemsFeature,
                                              IConnectionIdFeature,
                                              IConnectionTransportFeature,
                                              IConnectionHeartbeatFeature,
                                              IHttpContextFeature,
                                              IConnectionStatFeature
    {
        private const int CompletedState = 2;

        private const int IdleState = 0;

        private const int WritingState = 1;

        private static readonly PipeOptions DefaultPipeOptions = new PipeOptions(pauseWriterThreshold: 0,
            resumeWriterThreshold: 0,
            readerScheduler: PipeScheduler.ThreadPool,
            useSynchronizationContext: false);

        private readonly CancellationTokenSource _abortOutgoingCts = new CancellationTokenSource();

        private readonly TaskCompletionSource<object> _connectionEndTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly object _heartbeatLock = new object();

        private volatile bool _abortOnClose = true;

        private int _connectionState = IdleState;

        private List<(Action<object> handler, object state)> _heartbeatHandlers;

        private long _lastMessageReceivedAt;

        private long _receivedBytes;

        // Send "Abort" to service on close except that Service asks SDK to close
        public bool AbortOnClose
        {
            get => _abortOnClose;
            set => _abortOnClose = value;
        }

        public IDuplexPipe Application { get; set; }

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; }

        public HttpContext HttpContext { get; set; }

        public string InstanceId { get; }

        public bool IsMigrated { get; }

        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems(new ConcurrentDictionary<object, object>());

        public DateTime LastMessageReceivedAtUtc => new DateTime(Volatile.Read(ref _lastMessageReceivedAt), DateTimeKind.Utc);

        public Task LifetimeTask => _connectionEndTcs.Task;

        public CancellationToken OutgoingAborted => _abortOutgoingCts.Token;

        public string Protocol { get; }

        public long ReceivedBytes => Volatile.Read(ref _receivedBytes);

        public ServiceConnectionBase ServiceConnection { get; set; }

        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

        public override IDuplexPipe Transport { get; set; }

        public ClaimsPrincipal User { get; set; }

        public ServiceConnectionWritter WritterDelegate { get; set; }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public ClientConnectionContext(OpenConnectionMessage serviceMessage,
                                       Action<HttpContext> configureContext = null,
                                       PipeOptions transportPipeOptions = null,
                                       PipeOptions appPipeOptions = null)
        {
            ConnectionId = serviceMessage.ConnectionId;
            Protocol = serviceMessage.Protocol;
            User = serviceMessage.GetUserPrincipal();
            InstanceId = GetInstanceId(serviceMessage.Headers);

            // Create the Duplix Pipeline for the virtual connection
            transportPipeOptions = transportPipeOptions ?? DefaultPipeOptions;
            appPipeOptions = appPipeOptions ?? DefaultPipeOptions;

            var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
            Transport = pair.Application;
            Application = pair.Transport;

            HttpContext = BuildHttpContext(serviceMessage);
            configureContext?.Invoke(HttpContext);

            Features = BuildFeatures(serviceMessage);

            if (serviceMessage.Headers.TryGetValue(Constants.AsrsMigrateFrom, out _))
            {
                IsMigrated = true;
            }
        }

        /// <summary>
        /// Cancel the outgoing process
        /// </summary>
        public void CancelOutgoing(int millisecondsDelay = 0)
        {
            if (millisecondsDelay <= 0)
            {
                _abortOutgoingCts.Cancel();
            }
            else
            {
                _abortOutgoingCts.CancelAfter(millisecondsDelay);
            }
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
                if (_heartbeatHandlers == null)
                {
                    _heartbeatHandlers = new List<(Action<object> handler, object state)>();
                }
                _heartbeatHandlers.Add((action, state));
            }
        }

        public async Task ProcessApplicationTaskAsync(ConnectionDelegate connectionDelegate)
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

        public async Task ProcessOutgoingMessagesAsync()
        {
            var token = OutgoingAborted;

            try
            {
                if (IsMigrated)
                {
                    using var timeoutToken = new CancellationTokenSource(Constants.Periods.DefaultClientHandshakeTimeout);
                    using var source = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken.Token);

                    // A handshake response is not expected to be given
                    // if the connection was migrated from another server,
                    // since the connection hasn't been `dropped` from the client point of view.
                    if (!await SkipHandshakeResponse(source.Token))
                    {
                        return;
                    }
                }

                while (true)
                {
                    var result = await Application.Input.ReadAsync(token);

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
                            await WritterDelegate(new ConnectionDataMessage(ConnectionId, buffer));
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

                    Application.Input.AdvanceTo(buffer.End);
                }
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

        public async Task WriteMessageAsync(ReadOnlySequence<byte> payload)
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
                    var requestCulture = new RequestCulture(cultureName);
                    CultureInfo.CurrentCulture = requestCulture.Culture;
                    CultureInfo.CurrentUICulture = requestCulture.UICulture;
                }
                catch (Exception)
                {
                    // skip invalid culture, normal won't hit.
                }
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

        private HttpContext BuildHttpContext(OpenConnectionMessage message)
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

        private string GetInstanceId(IDictionary<string, StringValues> header)
        {
            if (header.TryGetValue(Constants.AsrsInstanceId, out var instanceId))
            {
                return instanceId;
            }
            return string.Empty;
        }

        private async Task<bool> SkipHandshakeResponse(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    var result = await Application.Input.ReadAsync(token);
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
                        Application.Input.AdvanceTo(buffer.Start);
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
    }
}
