// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
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
                                              IConnectionInherentKeepAliveFeature
    {
        public ServiceConnectionContext(OpenConnectionMessage serviceMessage)
        {
            ConnectionId = serviceMessage.ConnectionId;
            User = new ClaimsPrincipal();
            if (serviceMessage.Claims != null)
            {
                User.AddIdentity(new ClaimsIdentity(serviceMessage.Claims, "Bearer"));
            }

            // Create the Duplix Pipeline for the virtual connection
            var transportPipeOptions = new PipeOptions(pauseWriterThreshold: 0,
                resumeWriterThreshold: 0,
                readerScheduler: PipeScheduler.ThreadPool,
                useSynchronizationContext: false);
            var appPipeOptions = new PipeOptions(pauseWriterThreshold: 0,
                resumeWriterThreshold: 0,
                readerScheduler: PipeScheduler.ThreadPool,
                useSynchronizationContext: false);

            var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
            Transport = pair.Application;
            Application = pair.Transport;

            Features = new FeatureCollection();
            // Disable Ping for this virtual connection, set any TimeSpan is OK.
            Features.Set<IConnectionInherentKeepAliveFeature>(this);
            Features.Set<IConnectionUserFeature>(this);
            Features.Set<IConnectionItemsFeature>(this);
            Features.Set<IConnectionIdFeature>(this);
            Features.Set<IConnectionTransportFeature>(this);
        }

        // Mark the context in OnDisconnected state which means service has dropped the client connection
        public bool AbortOnClose { get; set; }

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; }

        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems(new ConcurrentDictionary<object, object>());

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        public ClaimsPrincipal User { get; set; }

        public Task ApplicationTask { get; set; }

        public bool HasInherentKeepAlive => true;

        // The associated HubConnectionContext
        public HubConnectionContext HubConnectionContext { get; set; }
    }
}
