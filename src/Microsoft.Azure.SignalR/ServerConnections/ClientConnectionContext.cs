// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Security.Claims;
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
        private static readonly PipeOptions DefaultPipeOptions = new PipeOptions(pauseWriterThreshold: 0,
            resumeWriterThreshold: 0,
            readerScheduler: PipeScheduler.ThreadPool,
            useSynchronizationContext: false);

        private readonly TaskCompletionSource<object> _connectionEndTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task CompleteTask => _connectionEndTcs.Task;

        public bool IsMigrated { get; }

        private readonly object _heartbeatLock = new object();
        private List<(Action<object> handler, object state)> _heartbeatHandlers;

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

        // Send "Abort" to service on close except that Service asks SDK to close
        public bool AbortOnClose { get; set; } = true;

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; }

        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems(new ConcurrentDictionary<object, object>());

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        public ClaimsPrincipal User { get; set; }

        public Task ApplicationTask { get; set; }

        public ServiceConnectionBase ServiceConnection { get; set; }

        public HttpContext HttpContext { get; set; }

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
