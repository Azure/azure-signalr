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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceConnectionContext : ConnectionContext,
                                              IConnectionUserFeature,
                                              IConnectionItemsFeature,
                                              IConnectionIdFeature,
                                              IConnectionTransportFeature,
                                              IConnectionHeartbeatFeature
    {
        private static readonly string[] SystemClaims =
        {
            "aud", // Audience claim, used by service to make sure token is matched with target resource.
            "exp", // Expiration time claims. A token is valid only before its expiration time.
            "iat", // Issued At claim. Added by default. It is not validated by service.
            "nbf"  // Not Before claim. Added by default. It is not validated by service.
        };

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
            transportPipeOptions = transportPipeOptions ?? new PipeOptions(pauseWriterThreshold: 0,
                resumeWriterThreshold: 0,
                readerScheduler: PipeScheduler.ThreadPool,
                useSynchronizationContext: false);

            appPipeOptions = appPipeOptions ?? new PipeOptions(pauseWriterThreshold: 0,
                resumeWriterThreshold: 0,
                readerScheduler: PipeScheduler.ThreadPool,
                useSynchronizationContext: false);

            var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
            Transport = pair.Application;
            Application = pair.Transport;

            Features = new FeatureCollection();
            Features.Set<IConnectionHeartbeatFeature>(this);
            Features.Set<IConnectionUserFeature>(this);
            Features.Set<IConnectionItemsFeature>(this);
            Features.Set<IConnectionIdFeature>(this);
            Features.Set<IConnectionTransportFeature>(this);
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

        private static bool IsAuthenticatedUser(Claim[] claims)
        {
            if (claims?.Length > 0)
            {
                foreach (var claim in claims)
                {
                    if (!SystemClaims.Contains(claim.Type))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
