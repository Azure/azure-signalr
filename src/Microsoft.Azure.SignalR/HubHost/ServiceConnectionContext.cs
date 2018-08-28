// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnectionContext : ConnectionContext,
                                              IConnectionUserFeature,
                                              IConnectionItemsFeature,
                                              IConnectionIdFeature,
                                              IConnectionTransportFeature,
                                              IConnectionHeartbeatFeature,
                                              IHttpContextFeature
    {
        private static readonly string[] SystemClaims =
        {
            "aud", // Audience claim, used by service to make sure token is matched with target resource.
            "exp", // Expiration time claims. A token is valid only before its expiration time.
            "iat", // Issued At claim. Added by default. It is not validated by service.
            "nbf"  // Not Before claim. Added by default. It is not validated by service.
        };

        private static readonly PipeOptions DefaultPipeOptions = new PipeOptions(pauseWriterThreshold: 0,
            resumeWriterThreshold: 0,
            readerScheduler: PipeScheduler.ThreadPool,
            useSynchronizationContext: false);

        private readonly object _heartbeatLock = new object();
        private List<(Action<object> handler, object state)> _heartbeatHandlers;

        public ServiceConnectionContext(OpenConnectionMessage serviceMessage, PipeOptions transportPipeOptions = null, PipeOptions appPipeOptions = null)
        {
            ConnectionId = serviceMessage.ConnectionId;
            User = new ClaimsPrincipal();
            User.AddIdentity(IsAuthenticatedUser(serviceMessage.Claims)
                ? new ClaimsIdentity(serviceMessage.Claims, "Bearer")
                : new ClaimsIdentity());

            // Create the Duplix Pipeline for the virtual connection
            transportPipeOptions = transportPipeOptions ?? DefaultPipeOptions;
            appPipeOptions = appPipeOptions ?? DefaultPipeOptions;

            var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
            Transport = pair.Application;
            Application = pair.Transport;

            HttpContext = BuildHttpContext(serviceMessage);

            Features = BuildFeatures();
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

        // The associated HubConnectionContext
        public HubConnectionContext HubConnectionContext { get; set; }

        public HttpContext HttpContext { get; set; }

        private static bool IsAuthenticatedUser(IReadOnlyCollection<Claim> claims)
        {
            return claims?.Count > 0 &&
                   claims.Any(claim =>
                       !SystemClaims.Contains(claim.Type) &&
                       !claim.Type.StartsWith(Constants.ClaimType.AzureSignalRSysPrefix, StringComparison.OrdinalIgnoreCase));
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

        private HttpContext BuildHttpContext(OpenConnectionMessage message)
        {
            var httpContextFeatures = new FeatureCollection();
            httpContextFeatures.Set<IHttpRequestFeature>(new HttpRequestFeature
            {
                Headers = new HeaderDictionary((Dictionary<string, StringValues>) message.Headers),
                QueryString = message.QueryString
            });
            httpContextFeatures.Set<IHttpAuthenticationFeature>(new HttpAuthenticationFeature
            {
                User = User
            });

            return new DefaultHttpContext(httpContextFeatures);
        }
    }
}
