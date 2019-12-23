﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionContext : ConnectionContext,
                                              IConnectionUserFeature,
                                              IConnectionItemsFeature,
                                              IConnectionIdFeature,
                                              IConnectionTransportFeature,
                                              IConnectionHeartbeatFeature,
                                              IHttpContextFeature
    {
        private const int WritingState = 1;
        private const int CompletingState = 2;
        private const int IdleState = 0;

        private static readonly PipeOptions DefaultPipeOptions = new PipeOptions(pauseWriterThreshold: 0,
            resumeWriterThreshold: 0,
            readerScheduler: PipeScheduler.ThreadPool,
            useSynchronizationContext: false);

        private readonly TaskCompletionSource<object> _connectionEndTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _abortOutgoingCts = new CancellationTokenSource();
        private readonly CancellationTokenSource _abortApplicationCts = new CancellationTokenSource();

        private readonly TaskCompletionSource<object> _writeCompleteTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _connectionState = IdleState;

        private readonly object _heartbeatLock = new object();

        private List<(Action<object> handler, object state)> _heartbeatHandlers;

        public Task CompleteTask => _connectionEndTcs.Task;

        public bool IsMigrated { get; }

        // Send "Abort" to service on close except that Service asks SDK to close
        public bool AbortOnClose { get; set; } = true;

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; }

        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems(new ConcurrentDictionary<object, object>());

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        public ClaimsPrincipal User { get; set; }

        public Task ApplicationTask { get; set; }

        public Task LifetimeTask { get; set; } = Task.CompletedTask;

        public ServiceConnectionBase ServiceConnection { get; set; }

        public HttpContext HttpContext { get; set; }

        public CancellationToken OutgoingAborted => _abortOutgoingCts.Token;

        public CancellationToken ApplicationAborted => _abortApplicationCts.Token;

        public ClientConnectionContext(OpenConnectionMessage serviceMessage, Action<HttpContext> configureContext = null, PipeOptions transportPipeOptions = null, PipeOptions appPipeOptions = null)
        {
            ConnectionId = serviceMessage.ConnectionId;
            User = serviceMessage.GetUserPrincipal();

            if (serviceMessage.Headers.TryGetValue(Constants.AsrsMigrateIn, out _))
            {
                IsMigrated = true;
            }

            // Create the Duplix Pipeline for the virtual connection
            transportPipeOptions = transportPipeOptions ?? DefaultPipeOptions;
            appPipeOptions = appPipeOptions ?? DefaultPipeOptions;

            var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
            Transport = pair.Application;
            Application = pair.Transport;

            HttpContext = BuildHttpContext(serviceMessage);
            configureContext?.Invoke(HttpContext);

            Features = BuildFeatures();
        }

        public async Task CompleteIncoming()
        {
            // always set the connection state to completing when this method is called
            var previousState =
                Interlocked.Exchange(ref _connectionState, CompletingState);
            
            Application.Output.CancelPendingFlush();

            if (previousState == WritingState)
            {
                // there is only when the connection is in writing that there is need to wait for write to complete
                await _writeCompleteTcs.Task;
            }

            Application.Output.Complete();
        }
        
        public async Task WriteMessageAsync(ReadOnlySequence<byte> payload)
        {
            var previousState = Interlocked.CompareExchange(ref _connectionState, WritingState, IdleState);
            
            // Write should not be called from multiple threads
            Debug.Assert(previousState != WritingState);

            if (previousState == CompletingState)
            {
                // already completing, don't write anymore
                return;
            }

            // Start write
            await WriteMessageAsyncCore(payload);

            // Try to set the connection to idle if it is in writing state, if it is in complete state, complete the tcs
            previousState = Interlocked.CompareExchange(ref _connectionState, IdleState, WritingState);
            if (previousState == CompletingState)
            {
                _writeCompleteTcs.TrySetResult(null);
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
            var requestFeature = new HttpRequestFeature
            {
                Headers = new HeaderDictionary((Dictionary<string, StringValues>)message.Headers),
                QueryString = message.QueryString,
                Path = GetOriginalPath(message.QueryString)
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

        private static string GetOriginalPath(string queryString)
        {
            var query = QueryHelpers.ParseNullableQuery(queryString);
            return query?.TryGetValue(Constants.QueryParameter.OriginalPath, out var originalPath) == true
                ? originalPath.FirstOrDefault()
                : string.Empty;
        }
    }
}
