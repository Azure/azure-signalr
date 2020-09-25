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
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;

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
                                              IHttpContextFeature
    {
        private const int WritingState = 1;
        private const int CompletedState = 2;
        private const int IdleState = 0;

        private static readonly PipeOptions DefaultPipeOptions = new PipeOptions(pauseWriterThreshold: 0,
            resumeWriterThreshold: 0,
            readerScheduler: PipeScheduler.ThreadPool,
            useSynchronizationContext: false);

        private readonly TaskCompletionSource<object> _connectionEndTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _abortOutgoingCts = new CancellationTokenSource();
        private readonly CancellationTokenSource _abortApplicationCts = new CancellationTokenSource();

        private int _connectionState = IdleState;

        private readonly object _heartbeatLock = new object();

        private List<(Action<object> handler, object state)> _heartbeatHandlers;

        private volatile bool _abortOnClose = true;

        public bool IsMigrated { get; }

        // Send "Abort" to service on close except that Service asks SDK to close
        public bool AbortOnClose
        {
            get => _abortOnClose;
            set => _abortOnClose = value;
        }

        public async Task WriteToClientAsync(ReadOnlyMemory<byte> appMessage)
        {
            await CurConn.serviceConnection.WriteAsync(new ConnectionDataMessage(CurConn.ConnectionId, appMessage));
        }

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; }

        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems(new ConcurrentDictionary<object, object>());

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        public ClaimsPrincipal User { get; set; }

        public Task LifetimeTask => _connectionEndTcs.Task;

        public ServiceConnectionBase ServiceConnection { get; set; }

        public HttpContext HttpContext { get; set; }

        public CancellationToken OutgoingAborted => _abortOutgoingCts.Token;

        public CancellationToken ApplicationAborted => _abortApplicationCts.Token;

        public bool HandshakeProcessed = false;

        public ClientConnectionContext(OpenConnectionMessage serviceMessage, Action<HttpContext> configureContext = null, PipeOptions transportPipeOptions = null, PipeOptions appPipeOptions = null)
        {
            ConnectionId = serviceMessage.ConnectionId;
            User = serviceMessage.GetUserPrincipal();

            // Create the Duplix Pipeline for the virtual connection
            transportPipeOptions = transportPipeOptions ?? DefaultPipeOptions;
            appPipeOptions = appPipeOptions ?? DefaultPipeOptions;

            var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
            Transport = pair.Application;
            Application = pair.Transport;

            HttpContext = BuildHttpContext(serviceMessage);
            configureContext?.Invoke(HttpContext);

            Features = BuildFeatures();

            if (serviceMessage.Headers.TryGetValue(Constants.AsrsMigrateFrom, out _))
            {
                IsMigrated = true;
            }

            BufferConnectionContext bcc = new BufferConnectionContext();
            bcc.ConnectionId = ConnectionId;
            bcc.ccc = this;
            bcc.receivedBarrier = true;
            bcc._ReloadTcs.SetResult(null);
            ConnectionMap[bcc.ConnectionId] = bcc;
            CurConn = bcc;
            Task.Run(() => bcc.ProcessIncoming(bcc.cts.Token));
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


        // Store backup clientConnetion context
        internal class BufferConnectionContext {
            internal string ConnectionId { get; set; }

            internal ClientConnectionContext ccc;

            internal bool receivedBarrier { get; set; }

            internal CancellationTokenSource cts = new CancellationTokenSource();

            internal ServiceConnectionBase serviceConnection { get; set; }

            internal readonly TaskCompletionSource<object> _ReloadTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            private readonly Channel<ReadOnlySequence<byte>> _intermediateChannel = Channel.CreateUnbounded<ReadOnlySequence<byte>>();

            //TODO: need add tcs for synchronization

            internal async Task WriteMessageAsync(ReadOnlySequence<byte> payload)
            {
                await _intermediateChannel.Writer.WriteAsync(payload);
            }

            internal async Task ProcessIncoming(CancellationToken token)
            {
                await _ReloadTcs.Task;
                
                while (await _intermediateChannel.Reader.WaitToReadAsync(token))
                {
                    while (_intermediateChannel.Reader.TryRead(out ReadOnlySequence<byte> payload))
                    {
                        Console.WriteLine("[Transport Layer]\tReceived message from connection: " + ConnectionId);
                        // Only WriteMessage received after barrier message.
                        await ccc.WriteMessageAsyncCore(payload);
                    }
                }
            }

        }

        public Queue<BufferConnectionContext> conns = new Queue<BufferConnectionContext>(); //Backup connection queue

        public Dictionary<string, BufferConnectionContext> ConnectionMap = new Dictionary<string, BufferConnectionContext>();

        public BufferConnectionContext CurConn { get; set; }

        public Task AddReloadConnection(string ConnectionId, ServiceConnection serviceConnection)
        {
            BufferConnectionContext bcc = new BufferConnectionContext();
            bcc.ConnectionId = ConnectionId;
            bcc.ccc = this;
            bcc.serviceConnection = serviceConnection;
            bcc.receivedBarrier = false;
            conns.Enqueue(bcc);
            ConnectionMap[bcc.ConnectionId] = bcc;
            Task.Run(() => bcc.ProcessIncoming(bcc.cts.Token));
            return Task.CompletedTask;
        }

        public async Task Switch()
        {
            // 1. Send Barrier
            // 2. Transport replaced by reload
            // 3. Wait until client send C`lientConnnectionAbort
            if (conns.Count > 0)
            {
                await conns.Peek()._ReloadTcs.Task;
                CurConn = conns.Dequeue();
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

        /// <summary>
        /// Cancel the application task
        /// </summary>
        public void CancelApplication(int millisecondsDelay = 0)
        {
            if (millisecondsDelay <= 0)
            {
                _abortApplicationCts.Cancel();
            }
            else
            {
                _abortApplicationCts.CancelAfter(millisecondsDelay);
            }
        }

        private FeatureCollection BuildFeatures()
        {
            var features = new FeatureCollection();
            features.Set<IConnectionHeartbeatFeature>(this);
            features.Set<IConnectionUserFeature>(this);
            features.Set<IConnectionItemsFeature>(this);
            features.Set<IConnectionIdFeature>(this);
            features.Set<IConnectionTransportFeature>(this);
            features.Set<IHttpContextFeature>(this);
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
    }
}
